using LootGod;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LootGod
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var path = Path.Combine(home, "lootgod.db");
			services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={path};"));

			services.AddCors(options =>
							options.AddDefaultPolicy(
								policy =>
									policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
			services.AddRouting();
		}

		public void Configure(IApplicationBuilder app, LootGodContext db)
		{
			db.Database.EnsureCreated();

			app.UseRouting();
			app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapGet("/", async (context) =>
				{
					await context.Response.WriteAsync("Hello World!");
				});

				endpoints.MapGet("GetLootRequests", async (context) =>
				{
					var db = context.RequestServices.GetRequiredService<LootGodContext>();
					var requests = await db.LootRequests.OrderByDescending(x => x.CreatedDate).ToListAsync();

					// use a DTO instead
					foreach (var x in requests) { x.IP = null; }

					await context.Response.WriteAsJsonAsync(requests);
				});

				endpoints.MapGet("GetLoots", async (context) =>
				{
					var db = context.RequestServices.GetRequiredService<LootGodContext>();
					var requests = await db.Loots.OrderBy(x => x.Name).ToListAsync();
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
					await context.Response.WriteAsJsonAsync(loot);
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

					await context.Response.WriteAsJsonAsync(item);
				});

				endpoints.MapPost("DeleteLootRequest", async (context) =>
				{
					var db = context.RequestServices.GetRequiredService<LootGodContext>();
					var id = int.Parse(context.Request.Query["id"]);
					var item = await db.LootRequests.SingleAsync(x => x.Id == id);
					db.LootRequests.Remove(item);
					_ = await db.SaveChangesAsync();
				});

				endpoints.MapPost("DeleteLoot", async (context) =>
				{
					var db = context.RequestServices.GetRequiredService<LootGodContext>();
					var id = int.Parse(context.Request.Query["id"]);
					var item = await db.Loots
						.Include(x => x.LootRequests)
						.SingleAsync(x => x.Id == id);
					db.LootRequests.RemoveRange(item.LootRequests);
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
			});
		}
	}
}
