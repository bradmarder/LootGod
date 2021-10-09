using LootGod;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var path = Path.Combine(home, "lootgod.db");
builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={path};"));
builder.Services.AddCors(options =>
				options.AddPolicy(
					"AllowAll",
					policy =>
						policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
using var foo = scope.ServiceProvider.GetRequiredService<LootGodContext>();
foo.Database.EnsureCreated();

app.UseCors("AllowAll");

app.MapGet("/", () => "Hello World!");

app.MapGet("GetLootRequests", async (context) =>
{
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var requests = await db.LootRequests.OrderByDescending(x => x.CreatedDate).ToListAsync();

	// use a DTO instead
	foreach (var x in requests) { x.IP = null; }

	await context.Response.WriteAsJsonAsync(requests);
});

app.MapGet("GetLoots", async (context) =>
{
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var requests = await db.Loots.OrderBy(x => x.Name).ToListAsync();
	await context.Response.WriteAsJsonAsync(requests);
});

app.MapPost("CreateLoot", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLoot>();
	ArgumentNullException.ThrowIfNull(dto);
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
	await context.Response.WriteAsJsonAsync(loot);
});

app.MapPost("CreateLootRequest", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLootRequest>();
	ArgumentNullException.ThrowIfNull(dto);
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

	await context.Response.WriteAsJsonAsync(item);
});

app.MapPost("DeleteLootRequest", async (context) =>
{
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var id = int.Parse(context.Request.Query["id"]);
	var item = await db.LootRequests.SingleAsync(x => x.Id == id);
	db.LootRequests.Remove(item);
	_ = await db.SaveChangesAsync();
});

app.MapPost("DeleteLoot", async (context) =>
{
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var id = int.Parse(context.Request.Query["id"]);
	var item = await db.Loots.SingleAsync(x => x.Id == id);
	db.Loots.Remove(item);
	_ = await db.SaveChangesAsync();
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

app.Run();


public class CreatePlayer
{
	public string Name { get; set; } = null!;
}
public class CreateLoot
{
	public byte Quantity { get; set; }
	public string Name { get; set; } = null!;
}