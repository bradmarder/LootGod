using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var source = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={source};"));
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddCors(options =>
		options.AddDefaultPolicy(policy =>
			policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(x => true)));
}

builder.Services.AddSignalR(e => e.EnableDetailedErrors = true);
builder.Services.AddScoped<LootService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);

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
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
	app.UseCors();
}
app.MapHub<LootHub>("/lootHub");
app.MapGet("/test", () => "Hello World!");

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

app.MapPost("CreateLoot", async (CreateLoot dto, LootGodContext db, LootService lootService) =>
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

	await lootService.RefreshLoots();
});

app.MapPost("CreateLootRequest", async (CreateLootRequest dto, LootGodContext db, HttpContext context, LootService lootService) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString();

	var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
	var requestCount = await db.LootRequests.CountAsync(x => x.IP == ip && x.CreatedDate > oneWeekAgo);
	if (requestCount > 100) { throw new Exception("Limit Break - More than 100 requests per week from single IP"); }

	var item = new LootRequest(dto, ip);
	_ = db.LootRequests.Add(item);
	_ = await db.SaveChangesAsync();

	await lootService.RefreshRequests();
});

app.MapPost("DeleteLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteDeleteAsync();

	await lootService.RefreshRequests();
});

app.MapPost("IncrementLootQuantity", async (LootGodContext db, int id, LootService lootService) =>
{
	await db.Loots
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quantity, y => y.Quantity + 1));

	await lootService.RefreshLoots();
});

app.MapPost("DecrementLootQuantity", async (LootGodContext db, int id, LootService lootService) =>
{
	await db.Loots
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quantity, y => y.Quantity == 0 ? 0 : y.Quantity - 1));

	await lootService.RefreshLoots();
});

app.MapPost("EnableLootLock", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString();
	_ = db.LootLocks.Add(new(true, ip));
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(true);
});

app.MapPost("DisableLootLock", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString();
	_ = db.LootLocks.Add(new(false, ip));
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(false);
});

app.MapGet("GetLootLock", async (LootGodContext db) =>
{
	var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

	return lootLock?.Lock ?? false;
});

app.MapPost("GrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, true));

	await lootService.RefreshRequests();
});

app.MapPost("UngrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, false));

	await lootService.RefreshRequests();
});

app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService) =>
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

	//await db.LootRequests
	//	.Where(x => !x.Archived)
	//	.ExecuteUpdateAsync(x => x
	//		.SetProperty(y => y.Archived, true)
	//		.SetProperty(y => y.Loot.Quantity, y => y.Granted ? y.Loot.Quantity - 1 : y.Loot.Quantity));

	var t1 = lootService.RefreshLoots();
	var t2 = lootService.RefreshRequests();
	await Task.WhenAll(t1, t2);
});

app.MapGet("GetGrantedLootOutput", async (LootGodContext db) =>
{
	var items = (await db.LootRequests
		.Include(x => x.Loot)
		.Where(x => x.Granted)
		.OrderBy(x => x.LootId)
		.ThenBy(x => x.MainName)
		.ToListAsync())
		.GroupBy(x => (x.LootId, x.MainName))
		.Select(x =>
		{
			var item = x.First();
			return $"{item.Loot.Name} | {item.MainName} | x{x.Sum(y => y.Quantity)}";
		});

	return string.Join(Environment.NewLine, items);
});

await app.RunAsync();

public class LootHub : Hub { }
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