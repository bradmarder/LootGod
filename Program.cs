using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSystemd();

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var path = Path.Combine(home, "lootgod.db");

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={path};"));
builder.Services.AddCors(options =>
	options.AddDefaultPolicy(policy =>
		policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(x => true)));
builder.Services.AddRouting();
builder.Services.AddSignalR(e => e.EnableDetailedErrors = true);

builder.WebHost.UseKestrel(x => x.ListenAnyIP(5000));

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	await db.Database.EnsureCreatedAsync();

	foreach (var item in StaticData.Loots.Concat(StaticData.ToLLoots))
	{
		if (!db.Loots.Any(x => x.Name == item))
		{
			db.Loots.Add(new(item));
		}
	}

	_ = await db.SaveChangesAsync();
}

// singleton
var hub = app.Services.GetRequiredService<IHubContext<LootHub>>();

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
app.MapHub<LootHub>("/lootHub");
app.MapGet("/", () => "Hello World!");

app.MapPost("login", async (CreateLoginAttempt dto, HttpContext context, LootGodContext db) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";

	_ = db.LoginAttempts.Add(new(dto.MainName, ip));
	_ = await db.SaveChangesAsync();

	return dto.Password == "test";
});

app.MapGet("GetLootRequests", async (LootGodContext db) =>
{
	return (await db.LootRequests
		.Where(x => !x.Archived)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.CharacterName)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetArchivedLootRequests", async (LootGodContext db, string? name, int? lootId) =>
{
	return (await db.LootRequests
		.Where(x => x.Archived)
		.Where(x => name == null || EF.Functions.Like(x.CharacterName, name))
		.Where(x => lootId == null || x.LootId == lootId)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.CharacterName)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetLoots", async (LootGodContext db) =>
{
	return (await db.Loots
		.OrderBy(x => x.Name)
		.ToListAsync())
		.Select(x => new LootDto(x));
});

app.MapPost("CreateLoot", async (CreateLoot dto, LootGodContext db, HttpContext context) =>
{
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

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("CreateLootRequest", async (CreateLootRequest dto, LootGodContext db, HttpContext context) =>
{
	//var exists = context.Request.Headers.TryGetValue("key", out var key);

	//if (!exists) { throw new UnauthorizedAccessException(); }

	//var player = await db.Players.SingleAsync(x => x.Key == key);
	var ip = context.Connection.RemoteIpAddress?.ToString();

	var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
	var requestCount = await db.LootRequests.CountAsync(x => x.IP == ip && x.CreatedDate > oneWeekAgo);
	if (requestCount > 100) { throw new Exception("Limit Break - More than 100 requests per week from single IP"); }

	var item = new LootRequest(dto, ip);
	_ = db.LootRequests.Add(item);
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("DeleteLootRequest", async (LootGodContext db, HttpContext context) =>
{
	var id = int.Parse(context.Request.Query["id"]);
	var item = await db.LootRequests.SingleAsync(x => x.Id == id);
	db.LootRequests.Remove(item);
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("DeleteLoot", async (LootGodContext db, HttpContext context) =>
{
	var id = int.Parse(context.Request.Query["id"]);
	var requests = await db.LootRequests.Where(x => x.LootId == id).ToListAsync();
	db.LootRequests.RemoveRange(requests);
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("EnableLootLock", async (LootGodContext db, HttpContext context) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString();
	_ = db.LootLocks.Add(new(true, ip));
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("DisableLootLock", async (LootGodContext db, HttpContext context) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString();
	_ = db.LootLocks.Add(new(false, ip));
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapGet("GetLootLock", async (LootGodContext db) =>
{
	var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

	return lootLock?.Lock ?? false;
});

app.MapPost("GrantLootRequest", async (LootGodContext db, HttpContext context) =>
{
	var id = int.Parse(context.Request.Query["id"]);
	var item = await db.LootRequests.SingleAsync(x => x.Id == id);
	item.Granted = true;
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("UngrantLootRequest", async (LootGodContext db, HttpContext context) =>
{
	var id = int.Parse(context.Request.Query["id"]);
	var item = await db.LootRequests.SingleAsync(x => x.Id == id);
	item.Granted = false;
	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapPost("FinishLootRequests", async (LootGodContext db) =>
{
	var items = await db.LootRequests
		.Include(x => x.Loot)
		.Where(x => !x.Archived)
		.ToListAsync();

	foreach (var x in items)
	{
		x.Archived = true;
	}
	foreach (var x in items.Where(x => x.Granted))
	{
		x.Loot.Quantity -= x.Quantity;
	}

	_ = await db.SaveChangesAsync();

	await LootHub.RefreshLoots(db, hub);
});

app.MapGet("GetGrantedLootOutput", async (LootGodContext db) =>
{
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

	return string.Join(Environment.NewLine, items);
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

await app.RunAsync();

public class CreateLoginAttempt
{
	public string MainName { get; set; } = null!;
	public string Password { get; set; } = null!;
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