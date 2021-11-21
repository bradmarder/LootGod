using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//builder.Host.UseSystemd();

builder.WebHost
	.UseKestrel()
	.UseUrls("http://*:5000")
	.ConfigureServices(services =>
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var path = Path.Combine(home, "lootgod.db");
		services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={path};"));

		services.AddCors(options =>
						options.AddDefaultPolicy(
							policy =>
								policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(x => true)));
		services.AddRouting();
		services.AddSignalR(e => e.EnableDetailedErrors = true);
	});

var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	await db.Database.EnsureCreatedAsync();

	foreach (var item in StaticData.Loots)
	{
		if (!db.Loots.Any(x => x.Name == item))
		{
			db.Loots.Add(new Loot(item));
		}
	}

	_ = await db.SaveChangesAsync();
}

app.UseExceptionHandler(opt =>
{
	opt.Run(async context =>
	{
		var ex = context.Features.Get<IExceptionHandlerFeature>();
		if (ex is not null)
		{
			var err = $"Error: {ex.Error.Message} {ex.Error.StackTrace } {ex.Error.InnerException?.Message}";
			await context.Response.WriteAsync(err);
			await Console.Out.WriteAsync(err);
		}
	});
});
app.UseRouting();
app.UseCors();
app.UseEndpoints(endpoints =>
{
	endpoints.MapHub<LootHub>("/lootHub");
	endpoints.MapGet("/", async (context) =>
	{
		await context.Response.WriteAsync("Hello World!");
	});

	endpoints.MapPost("login", async (context) =>
	{
		var ip = context.Connection.RemoteIpAddress?.ToString();
		if (ip is null) { return; }

		var dto = await context.Request.ReadFromJsonAsync<CreateLoginAttempt>();
		if (dto is null) { throw new ArgumentNullException(nameof(dto)); }

		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		_ = db.LoginAttempts.Add(new(dto.MainName, ip));
		_ = await db.SaveChangesAsync();
	});

	endpoints.MapGet("GetLootRequests", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var requests = (await db.LootRequests
			.OrderByDescending(x => x.Spell)
			.ThenBy(x => x.LootId)
			.ThenByDescending(x => x.CharacterName)
			.ToListAsync())
			.Select(x => new LootRequestDto(x));

		await context.Response.WriteAsJsonAsync(requests);
	});

	endpoints.MapGet("GetLoots", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var requests = (await db.Loots
			.OrderBy(x => x.Name)
			.ToListAsync())
			.Select(x => new LootDto(x));

		await context.Response.WriteAsJsonAsync(requests);
	});

	endpoints.MapPost("CreateLoot", async (context) =>
	{
		var dto = await context.Request.ReadFromJsonAsync<CreateLoot>();
		//ArgumentNullException.ThrowIfNull(dto);
		if (dto is null) { throw new Exception(); }
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var loot = await db.Loots.SingleOrDefaultAsync(x => x.Name == dto.Name) ?? new(dto);
		if (loot.Id == 0)
		{
			_ = db.Loots.Add(loot);
		}
		else
		{
			loot.Quantity = dto.Quantity;
		}
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("CreateLootRequest", async (context) =>
	{
		var dto = await context.Request.ReadFromJsonAsync<CreateLootRequest>();
		//ArgumentNullException.ThrowIfNull(dto);=
		if (dto is null) { throw new Exception(); }
		//var exists = context.Request.Headers.TryGetValue("key", out var key);

		//if (!exists) { throw new UnauthorizedAccessException(); }

		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		//var player = await db.Players.SingleAsync(x => x.Key == key);
		var ip = context.Connection.RemoteIpAddress?.ToString();

		var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
		var requestCount = await db.LootRequests.CountAsync(x => x.IP == ip && x.CreatedDate > oneWeekAgo);
		if (requestCount > 100) { throw new Exception("Limit Break - More than 100 requests per week from single IP"); }

		var item = new LootRequest(dto, ip);
		_ = db.LootRequests.Add(item);
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("DeleteLootRequest", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var id = int.Parse(context.Request.Query["id"]);
		var item = await db.LootRequests.SingleAsync(x => x.Id == id);
		db.LootRequests.Remove(item);
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("DeleteLoot", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var id = int.Parse(context.Request.Query["id"]);
		var requests = await db.LootRequests.Where(x => x.LootId == id).ToListAsync();
		db.LootRequests.RemoveRange(requests);
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("EnableLootLock", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var ip = context.Connection.RemoteIpAddress?.ToString();
		_ = db.LootLocks.Add(new(true, ip));
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("DisableLootLock", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var ip = context.Connection.RemoteIpAddress?.ToString();
		_ = db.LootLocks.Add(new(false, ip));
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapGet("GetLootLock", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

		await context.Response.WriteAsJsonAsync(lootLock?.Lock ?? false);
	});

	endpoints.MapPost("GrantLootRequest", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var id = int.Parse(context.Request.Query["id"]);
		var item = await db.LootRequests.SingleAsync(x => x.Id == id);
		item.Granted = true;
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("UngrantLootRequest", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var id = int.Parse(context.Request.Query["id"]);
		var item = await db.LootRequests.SingleAsync(x => x.Id == id);
		item.Granted = false;
		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapPost("FinishLootRequests", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var items = await db.LootRequests
			.Include(x => x.Loot)
			.ToListAsync();

		foreach (var x in items.Where(x => x.Granted))
		{
			x.Loot.Quantity -= x.Quantity;
		}

		// delete *ALL* loot requests
		db.LootRequests.RemoveRange(items);

		_ = await db.SaveChangesAsync();

		await LootHub.RefreshLoots(context);
	});

	endpoints.MapGet("GetGrantedLootOutput", async (context) =>
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var items = (await db.LootRequests
			.Include(x => x.Loot)
			.Where(x => x.Granted)
			.OrderBy(x => x.LootId)
			.ThenBy(x => x.MainName)
			.ThenBy(x => x.CharacterName)
			.ToListAsync())
			.GroupBy(x => (x.LootId, x.MainName, x.CharacterName))
			.Select(x =>
			{
				var item = x.First();
				return $"{item.Loot.Name} | {item.MainName} ({item.CharacterName}) | x{x.Sum(y => y.Quantity)}";
			});
		var output = string.Join(Environment.NewLine, items);

		await context.Response.WriteAsync(output);
	});

	//app.MapPost("CreatePlayer", async (context) =>
	//{
	//	var dto = await context.Request.ReadFromJsonAsync<CreatePlayer>();
	//	ArgumentNullException.ThrowIfNull(dto);
	//	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	//	var player = new Player(dto);
	//	_ = db.Players.Add(player);
	//	_ = await db.SaveChangesAsync();

	//	await context.Response.WriteAsync(player.Key);
	//});
});

app.Run();

public class CreateLoginAttempt
{
	public string MainName { get; set; } = null!;
}
public class CreatePlayer
{
	public string Name { get; set; } = null!;
}
public class CreateLoot
{
	public byte Quantity { get; set; }
	public string Name { get; set; } = null!;
}