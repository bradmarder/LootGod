using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	public class LootRequestDto
	{
		public LootRequestDto(LootRequest model)
		{
			Id = model.Id;
			CreatedDate = model.CreatedDate;
			IP = model.IP;
			MainName = model.MainName;
			CharacterName = model.CharacterName;
			Class = model.Class;
			Spell = model.Spell;
			LootId = model.LootId;
			Quantity = model.Quantity;
			IsAlt = model.IsAlt;
		}

		public int Id { get; }
		public DateTime CreatedDate { get; }
		public string? IP { get; }
		public string MainName { get; }
		public string CharacterName { get; }
		public string? Spell { get; }
		public EQClass Class { get; }
		public int LootId { get; }
		public int Quantity { get; }
		public bool IsAlt { get; }
	}
	public class LootDto
	{
		public LootDto(Loot model)
		{
			Id = model.Id;
			Quantity = model.Quantity;
			Name = model.Name;
			IsSpell = model.IsSpell;
		}

		public int Id { get; }
		public byte Quantity { get; }
		public string Name { get; }
		public bool IsSpell { get; }
	}

	public class LootHub : Hub
	{
		public static async Task RefreshLoots(HttpContext context)
		{
			var db = context.RequestServices.GetRequiredService<LootGodContext>();
			var loots = await db.Loots.OrderBy(x => x.Name).ToListAsync();
			var requests = await db.LootRequests.OrderByDescending(x => x.CreatedDate).ToListAsync();
			var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

			var hub = context.RequestServices.GetRequiredService<IHubContext<LootHub>>();
			await hub.Clients.All.SendAsync("refresh",
				lootLock?.Lock ?? false,
				loots.Select(x => new LootDto(x)),
				requests.Select(x => new LootRequestDto(x)));
		}
	}
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
									policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(x => true)));
			services.AddRouting();
			services.AddSignalR(e => e.EnableDetailedErrors = true);
		}

		public void Configure(IApplicationBuilder app, LootGodContext db)
		{
			db.Database.EnsureCreated();

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
						.OrderByDescending(x => x.CreatedDate)
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
					var item = await db.Loots
						.Include(x => x.LootRequests)
						.SingleAsync(x => x.Id == id);
					db.LootRequests.RemoveRange(item.LootRequests);
					db.Loots.Remove(item);
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
