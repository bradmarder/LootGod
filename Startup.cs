using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
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
			Granted = model.Granted;
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
		public bool Granted { get; }
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
			var requests = await db.LootRequests
				.OrderByDescending(x => x.Spell)
				.ThenBy(x => x.LootId)
				.ThenByDescending(x => x.CharacterName)
				.ToListAsync();
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

		private static readonly HashSet<string> _loots = new HashSet<string>
		{
			"Captured Essence of Ethernere",
			"Glowing Dragontouched Rune",
			"Greater Dragontouched Rune",
			"Lesser Dragontouched Rune",
			"Median Dragontouched Rune",
			"Minor Dragontouched Rune",
			"Ashrin, Last Defense",
			"Axe of Draconic Legacy",
			"Blazing Spear",
			"Bloodied Talisman",
			"Bow of Living Frost",
			"Braided Belt of Dragonshide",
			"Cloak of Mortality",
			"Commanding Blade",
			"Crusaders' Cowl",
			"Diamondized Restless Ore",
			"Dragonsfire Staff",
			"Drakescale Mask",
			"Drape of Dust,Shoulders",
			"Dreamweaver's Axe of Banishment",
			"Drop of Klandicar's Blood",
			"Faded Hoarfrost Arms Armor",
			"Faded Hoarfrost Chest Armor",
			"Faded Hoarfrost Feet Armor",
			"Faded Hoarfrost Hands Armor",
			"Faded Hoarfrost Head Armor",
			"Faded Hoarfrost Legs Armor",
			"Faded Hoarfrost Wrist Armor",
			"First Brood Signet Ring",
			"Flame Touched Velium Slac",
			"Flametongue, Uiliak's Menace",
			"Frigid Runic Sword",
			"Frosted Scale",
			"Frozen Foil of the New Brood",
			"Frozen Gutripper",
			"Ganzito's Crystal Ring",
			"Glowing Icicle",
			"Guard of Echoes",
			"Imbued Hammer of Skyshrine",
			"Jchan's Threaded Belt of Command",
			"Klanderso's Stabber of Slaughter",
			"Linked Belt of Entrapment",
			"Mace of Crushing Energy",
			"Mace of Flowing Life",
			"Mantle of Mortality",
			"Mastodon Hide Mask",
			"Morrigan's Trinket of Fate",
			"Mrtyu's Rod of Disempowerment",
			"New Brood Talisman",
			"Niente's Dagger of Knowledge",
			"Nintal's Intricate Buckler",
			"Pendant of Whispers",
			"Pip's Cloak of Trickery",
			"Polished Ivory Katar",
			"Quoza's Amulet",
			"Ratalthor's Earring of Insight",
			"Redscale Cloak,Back",
			"Restless Ice Shard",
			"Ring of True Echoes",
			"Scale-Plated Crate",
			"Scepter of the Banished",
			"Soul Banisher",
			"Starseed Bow",
			"Starsight",
			"Suspended Scale Earring",
			"Tantor's Eye",
			"Tears of the Final Stand",
			"Tusk of Frost",
			"Twilight, Staff of the Exiled",
			"Zieri's Shawl of Compassion",
		};

		public void Configure(IApplicationBuilder app, LootGodContext db)
		{
			db.Database.EnsureCreated();
			foreach (var item in _loots)
			{
				if (!db.Loots.Any(x => x.Name == item))
				{
					db.Loots.Add(new Loot(item));
				}
			}
			db.SaveChanges();

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
