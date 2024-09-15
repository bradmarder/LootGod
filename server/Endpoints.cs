using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text;

public class Endpoints(string _adminKey)
{
	static void EnsureSingle(int rows)
	{
		if (rows is not 1)
		{
			throw new Exception("Expected 1 row updated, actual = " + rows);
		}
	}

	void EnsureOwner(string key)
	{
		if (key != _adminKey)
		{
			throw new UnauthorizedAccessException(key);
		}
	}

	public static bool IsValidDiscordWebhook(string webhook)
	{
		return Uri.TryCreate(webhook, UriKind.Absolute, out var uri) && uri is
		{
			Port: 443,
			Scheme: "https",
			Host: "discord.com",
			Fragment: "",
			Query: "",
			Segments: ["/", "api/", "webhooks/", var x, { Length: 68 }],
		}
		&& long.TryParse(x[..^1], out _);
	}

	public void Map(IEndpointRouteBuilder app)
	{
		app.MapGet("SSE", async (HttpContext ctx, IServiceScopeFactory factory, ConcurrentDictionary<string, DataSink> dataSinks) =>
		{
			var res = ctx.Response;
			var token = ctx.RequestAborted;
			var connectionId = ctx.Connection.Id ?? "";

			// avoid keeping a persistant reference to LootService -> DbContext
			await using (var scope = factory.CreateAsyncScope())
			{
				scope.ServiceProvider
					.GetRequiredService<LootService>()
					.AddDataSink(connectionId, res, token);
			}

			token.Register(() => dataSinks.Remove(connectionId, out _));

			res.Headers.Append("Content-Type", "text/event-stream");
			res.Headers.Append("Cache-Control", "no-cache");
			res.Headers.Append("Connection", "keep-alive");

			await res.WriteAsync("data: empty\n\n\n", token);
			await res.Body.FlushAsync(token);
			await Task.Delay(Timeout.InfiniteTimeSpan, token);
		});

		app.MapPost("GuildDiscord", Results<Ok, BadRequest<string>> (LootGodContext db, LootService lootService, UpdateGuildDiscord dto) =>
		{
			lootService.EnsureGuildLeader();

			if (!string.IsNullOrEmpty(dto.Webhook) && !IsValidDiscordWebhook(dto.Webhook))
			{
				return TypedResults.BadRequest("Invalid Discord Webhook Format");
			}

			var guildId = lootService.GetGuildId();
			var rows = db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => dto.RaidNight ? y.RaidDiscordWebhookUrl : y.RotDiscordWebhookUrl, dto.Webhook));
			EnsureSingle(rows);

			return TypedResults.Ok();
		});

		app.MapGet("Vacuum", (LootGodContext db) => db.Database.ExecuteSqlRaw("VACUUM"));

		app.MapGet("Backup", (HttpContext ctx, LootGodContext db, TimeProvider time, string key) =>
		{
			EnsureOwner(key);

			var now = time.GetUtcNow().ToUnixTimeSeconds();
			var tempFileName = Path.GetTempFileName();
			db.Database.ExecuteSqlRaw("VACUUM INTO {0}", tempFileName);

			// ensure the backup temp file is deleted once the request finishes processing
			var delete = new SelfDestruct(tempFileName);
			ctx.Response.RegisterForDispose(delete);

			return Results.File(tempFileName, fileDownloadName: $"backup-{now}.db");
		});

		app.MapGet("GetLootRequests", (LootService lootService) =>
		{
			var guildId = lootService.GetGuildId();

			return lootService.LoadLootRequests(guildId);
		});

		app.MapGet("GetArchivedLootRequests", (LootGodContext db, LootService lootService, string? name, int? itemId) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			return db.LootRequests
				.Where(x => x.Player.GuildId == EF.Constant(guildId))
				.Where(x => x.Archived)
				.Where(x => name == null || x.AltName!.StartsWith(name) || x.Player.Name.StartsWith(name))
				.Where(x => itemId == null || x.ItemId == itemId)
				.OrderByDescending(x => x.CreatedDate)
				.Select(x => new LootRequestDto
				{
					Id = x.Id,
					PlayerId = x.PlayerId,
					CreatedDate = x.CreatedDate,
					AltName = x.AltName,
					MainName = x.Player.Name,
					Class = x.Class ?? x.Player.Class,
					Spell = x.Spell,
					ItemId = x.ItemId,
					LootName = x.Item.Name,
					Quantity = x.Quantity,
					RaidNight = x.RaidNight,
					IsAlt = x.IsAlt,
					Granted = x.Granted,
					CurrentItem = x.CurrentItem,
				})
				.ToArray();
		});

		// TODO: remove? lockdown somehow
		app.MapPost("CreateItem", async (LootGodContext db, string name, LootService lootService) =>
		{
			var item = new Item(name, Expansion.LS);
			db.Items.Add(item);
			db.SaveChanges();

			await lootService.RefreshItems();
		});

		app.MapGet("FreeTrade", (LootGodContext db, LootService lootService) =>
		{
			var guildId = lootService.GetGuildId();

			return db.Guilds.Any(x => x.Id == EF.Constant(guildId) && x.Server == Server.FirionaVie);
		});

		app.MapGet("GetDiscordWebhooks", (LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var guild =  db.Guilds.Single(x => x.Id == EF.Constant(guildId));

			return new Hooks(guild.RaidDiscordWebhookUrl ?? "", guild.RotDiscordWebhookUrl ?? "");
		});

		app.MapGet("GetItems", (LootService lootService) =>
		{
			//lootService.EnsureAdminStatus();

			return lootService.LoadItems();
		});

		app.MapGet("GetLoots", (LootService lootService) =>
		{
			var guildId = lootService.GetGuildId();

			return lootService.LoadLoots(guildId);
		});

		app.MapPost("ToggleHiddenPlayer", (ToggleHiddenAdminPlayer dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			var rows = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Name == dto.Name)
				.ExecuteUpdate(x => x.SetProperty(y => y.Hidden, y => !y.Hidden));
			EnsureSingle(rows);
		});

		app.MapPost("TogglePlayerAdmin", (ToggleHiddenAdminPlayer dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();

			var rows = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Name == dto.Name)
				.ExecuteUpdate(x => x.SetProperty(y => y.Admin, y => !y.Admin));
			EnsureSingle(rows);
		});

		app.MapPost("CreateLootRequest", async (CreateLootRequest dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureRaidLootUnlocked();

			var guildId = lootService.GetGuildId();
			var playerId = lootService.GetPlayerId();

			// remove AltName if it matches main name
			var playerName = db.Players.Single(x => x.Id == playerId).Name;
			if (StringComparer.OrdinalIgnoreCase.Equals(playerName, dto.AltName?.Trim()))
			{
				dto = dto with { AltName = null };
			}

			var ip = lootService.GetIPAddress();
			var request = new LootRequest(dto, ip, playerId);
			db.LootRequests.Add(request);
			db.SaveChanges();

			await lootService.RefreshRequests(guildId);
		});

		app.MapDelete("DeleteLootRequest", async (LootGodContext db, int id, LootService lootService) =>
		{
			lootService.EnsureRaidLootUnlocked();

			var request = db.LootRequests.Single(x => x.Id == id);
			var guildId = lootService.GetGuildId();
			var playerId = lootService.GetPlayerId();
			if (request.PlayerId != playerId)
			{
				throw new UnauthorizedAccessException($"PlayerId {playerId} does not have access to loot id {id}");
			}
			if (request.Archived)
			{
				throw new Exception("Cannot delete archived loot requests");
			}

			db.LootRequests.Remove(request);
			db.SaveChanges();

			await lootService.RefreshRequests(guildId);
		});

		app.MapPost("UpdateLootQuantity", async (CreateLoot dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			var loot = db.Loots.SingleOrDefault(x => x.GuildId == guildId && x.ItemId == dto.ItemId);
			if (loot is null)
			{
				loot = new Loot(guildId, dto.ItemId);
				db.Loots.Add(loot);
			}
			_ = dto.RaidNight
				? loot.RaidQuantity = dto.Quantity
				: loot.RotQuantity = dto.Quantity;

			// if quantities are zero, then remove the loot record
			if (loot.RaidQuantity is 0 && loot.RotQuantity is 0)
			{
				db.Loots.Remove(loot);
			}

			db.SaveChanges();

			await lootService.RefreshLoots(guildId);
		});

		app.MapPost("CreateGuild", (LootGodContext db, CreateGuild dto) =>
		{
			var player = new Player(dto.LeaderName, dto.GuildName, dto.Server);
			db.Players.Add(player);
			db.SaveChanges();

			return player.Key!.Value;
		});

		app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, LootLock lootLock) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			var rows = db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.LootLocked, lootLock.Enable));
			EnsureSingle(rows);

			await lootService.RefreshLock(guildId, lootLock.Enable);
		});

		app.MapGet("GetLinkedAlts", (LootGodContext db, LootService lootService) =>
		{
			var playerId = lootService.GetPlayerId();
			var guildId = lootService.GetGuildId();

			return db.Players
				.Where(x => x.GuildId == EF.Constant(guildId))
				.Where(x => x.MainId == playerId)
				.Select(x => x.Name)
				.ToArray();
		});

		app.MapPost("LinkAlt", (LootGodContext db, LootService lootService, string altName) =>
		{
			var playerId = lootService.GetPlayerId();
			var guildId = lootService.GetGuildId();
			var validAltName = char.ToUpperInvariant(altName[0]) + altName.Substring(1).ToLowerInvariant();

			var rows = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Alt == true)
				.Where(x => x.MainId == null)
				.Where(x => x.Name == validAltName)
				.ExecuteUpdate(x => x.SetProperty(y => y.MainId, playerId));
			EnsureSingle(rows);
		});

		app.MapGet("GetLootLock", (LootService x) => x.GetRaidLootLock());
		app.MapGet("GetPlayerId", (LootService x) => x.GetPlayerId());
		app.MapGet("GetAdminStatus", (LootService x) => x.GetAdminStatus());
		app.MapGet("GetLeaderStatus", (LootService x) => x.IsGuildLeader());

		app.MapGet("GetMessageOfTheDay", (LootService lootService, LootGodContext db) =>
		{
			var guildId = lootService.GetGuildId();
			var guild = db.Guilds.Single(x => x.Id == EF.Constant(guildId));

			return guild.MessageOfTheDay ?? "";
		});

		app.MapPost("UploadMessageOfTheDay", async (LootService lootService, LootGodContext db, MessageOfTheDay dto) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var rows = db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.MessageOfTheDay, dto.Message));
			EnsureSingle(rows);

			await lootService.RefreshMessageOfTheDay(guildId, dto.Message);
		});

		app.MapPost("GrantLootRequest", async (LootGodContext db, LootService lootService, GrantLootRequest dto) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			var rows = db.LootRequests
				.Where(x => x.Id == dto.Id)
				.Where(x => x.Player.GuildId == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.Granted, dto.Grant));
			EnsureSingle(rows);

			await lootService.RefreshRequests(guildId);
		});

		app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService, FinishLoots finish) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			// capture the output before we archive requests
			var output = lootService.GetGrantedLootOutput(finish.RaidNight);

			var requests = db.LootRequests
				.Where(x => x.Player.GuildId == guildId)
				.Where(x => !x.Archived)
				.Where(x => x.RaidNight == finish.RaidNight)
				.ToList();
			var loots = db.Loots
				.Where(x => x.GuildId == guildId)
				.Where(x => (finish.RaidNight ? x.RaidQuantity : x.RotQuantity) > 0)
				.ToList();

			foreach (var request in requests)
			{
				request.Archived = true;
			}
			foreach (var loot in loots)
			{
				var grantedQuantity = requests
					.Where(x => x.ItemId == loot.ItemId && x.Granted)
					.Sum(x => x.Quantity);

				if (finish.RaidNight)
				{
					loot.RotQuantity += (byte)(loot.RaidQuantity - grantedQuantity);
					loot.RaidQuantity = 0;
				}
				else
				{
					loot.RotQuantity -= (byte)grantedQuantity;
				}

				// if quantities are zero, then remove the loot record
				if (loot.RaidQuantity is 0 && loot.RotQuantity is 0)
				{
					db.Loots.Remove(loot);
				}
			}

			db.SaveChanges();

			var guild = db.Guilds.Single(x => x.Id == guildId);
			var webhook = finish.RaidNight ? guild.RaidDiscordWebhookUrl : guild.RotDiscordWebhookUrl;
			if (webhook is not null)
			{
				await lootService.DiscordWebhook(output, webhook);
			}

			await lootService.RefreshLoots(guildId);
			await lootService.RefreshRequests(guildId);
		});

		app.MapPost("TransferGuildLeadership", Results<Ok, BadRequest<string>> (LootGodContext db, LootService lootService, TransferGuildName dto) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var leaderId = lootService.GetPlayerId();
			var oldLeader = db.Players.Single(x => x.Id == leaderId);
			var newLeader = db.Players.SingleOrDefault(x => x.GuildId == guildId && x.Name == dto.Name);

			if (newLeader is null)
			{
				return TypedResults.BadRequest($"No guild member with name '{dto.Name}' found.");
			}
			if (oldLeader.Id == newLeader.Id)
			{
				return TypedResults.BadRequest("Cannot transfer guild leadership to self?!");
			}

			newLeader.Admin = true;
			newLeader.RankId = oldLeader.RankId;
			oldLeader.RankId = null;

			db.SaveChanges();

			return TypedResults.Ok();
		});

		app.MapPost("ImportDump", async Task<Results<Ok, BadRequest<string>>> (LootService lootService, IFormFile file, int offset) =>
		{
			lootService.EnsureAdminStatus();

			var ext = Path.GetExtension(file.FileName);
			var import = (ext, file.FileName) switch
			{
				(".zip", _) => lootService.BulkImportRaidDump(file, offset),
				(".txt", var x) when x.StartsWith("RaidRoster") => lootService.ImportRaidDump(file, offset),
				(".txt", var x) when x.Split('-').Length is 3 => lootService.ImportGuildDump(file),
				_ => Task.FromException(new ImportException($"Cannot determine import dump for filename '{file.FileName}'"))
			};
			try
			{
				await import;
				return TypedResults.Ok();
			}
			catch (ImportException ex)
			{
				return TypedResults.BadRequest(ex.Message);
			}
		}).DisableAntiforgery();

		app.MapGet("GetPlayerAttendance", (LootGodContext db, LootService lootService, TimeProvider time) =>
		{
			var guildId = lootService.GetGuildId();
			var now = time.GetUtcNow();
			var oneHundredEighty = now.AddDays(-180);
			var ninety = now.AddDays(-90);
			var thirty = now.AddDays(-30);
			var ninetyDaysAgo = DateOnly.FromDateTime(ninety.DateTime);
			var thirtyDaysAgo = DateOnly.FromDateTime(thirty.DateTime);
			var oneHundredEightyDaysAgo = DateOnly.FromDateTime(oneHundredEighty.DateTime);
			var threshold = oneHundredEighty.ToUnixTimeSeconds();

			var playerMap = db.Players
				.AsNoTracking()
				.Where(x => x.GuildId == EF.Constant(guildId))
				.Where(x => x.Active == true)
				.ToDictionary(x => x.Id);
			var altMainMap = playerMap
				.Where(x => x.Value.MainId is not null)
				.ToDictionary(x => x.Key, x => x.Value.MainId!.Value);
			var rankIdToNameMap = db.Ranks
				.Where(x => x.GuildId == EF.Constant(guildId))
				.ToDictionary(x => x.Id, x => x.Name);
			var dumps = db.RaidDumps
				.AsNoTracking()
				.Where(x => x.Timestamp > threshold)
				.Where(x => x.Player.GuildId == EF.Constant(guildId))
				.Where(x => x.Player.Active == true)
				.ToList();
			var uniqueDates = dumps
				.Select(x => DateTimeOffset.FromUnixTimeSeconds(x.Timestamp))
				.Select(x => DateOnly.FromDateTime(x.DateTime))
				.ToHashSet();
			var oneHundredEightDayMaxCount = uniqueDates.Count;
			var ninetyDayMaxCount = uniqueDates.Count(x => x >= ninetyDaysAgo);
			var thirtyDayMaxCount = uniqueDates.Count(x => x >= thirtyDaysAgo);

			// if there are zero raid dumps for mains, include them in RA
			//playerMap
			//	.Select(x => x.Value)
			//	.Where(x => x.MainId is null)
			//	.Where(x => x.Alt != true)
			//	.ToList()
			//	.ForEach(x => raidPlayers.TryAdd(x, new()));

			static byte GetPercent(IEnumerable<DateOnly> values, DateOnly daysAgo, int max)
			{
				return (byte)(max is 0 ? 0 : Math.Round(100d * values.Count(y => y >= daysAgo) / max, 0, MidpointRounding.AwayFromZero));
			}

			return dumps
				.Select(x => altMainMap.TryGetValue(x.PlayerId, out var mainId)
					? new RaidDump(x.Timestamp, mainId)
					: x)
				.GroupBy(x => x.PlayerId)
				.ToDictionary(
					x => playerMap[x.Key],
					x => x
						.Select(y => DateTimeOffset.FromUnixTimeSeconds(y.Timestamp))
						.Select(y => DateOnly.FromDateTime(y.DateTime))
						.ToHashSet())
				.Select(x => new RaidAttendanceDto
				{
					Name = x.Key.Name,
					Hidden = x.Key.Hidden,
					Admin = x.Key.Admin,
					Rank = x.Key.RankId is null ? "unknown" : rankIdToNameMap[x.Key.RankId.Value],

					_30 = GetPercent(x.Value, thirtyDaysAgo, thirtyDayMaxCount),
					_90 = GetPercent(x.Value, ninetyDaysAgo, ninetyDayMaxCount),
					_180 = GetPercent(x.Value, oneHundredEightyDaysAgo, oneHundredEightDayMaxCount),
				})
				.OrderBy(x => x.Name)
				.ToArray();
		});

		app.MapGet("GetPlayerAttendance_V2", (LootGodContext db, LootService lootService, TimeProvider time) =>
		{
			var guildId = lootService.GetGuildId();
			var now = time.GetUtcNow();
			var oneHundredEightyDaysAgo = now.AddDays(-180).ToUnixTimeSeconds();
			var ninetyDaysAgo = now.AddDays(-90).ToUnixTimeSeconds();
			var thirtyDaysAgo = now.AddDays(-30).ToUnixTimeSeconds();

			var playerMap = db.Players
				.AsNoTracking()
				.Where(x => x.GuildId == EF.Constant(guildId))
				.Where(x => x.Active == true)
				.ToDictionary(x => x.Id);
			var altMainMap = playerMap
				.Where(x => x.Value.MainId is not null)
				.ToDictionary(x => x.Key, x => x.Value.MainId!.Value);
			var rankIdToNameMap = db.Ranks
				.Where(x => x.GuildId == EF.Constant(guildId))
				.ToDictionary(x => x.Id, x => x.Name);
			var dumps = db.RaidDumps
				.AsNoTracking()
				.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
				.Where(x => x.Player.GuildId == EF.Constant(guildId))
				.Where(x => x.Player.Active == true)
				.ToList();
			var uniqueDates = dumps
				.Select(x => x.Timestamp)
				.ToHashSet();
			var oneHundredEightDayMaxCount = uniqueDates.Count;
			var ninetyDayMaxCount = uniqueDates.Count(x => x >= ninetyDaysAgo);
			var thirtyDayMaxCount = uniqueDates.Count(x => x >= thirtyDaysAgo);

			static byte GetPercent(IEnumerable<long> values, long daysAgo, int max)
			{
				return (byte)(max is 0 ? 0 : Math.Round(100d * values.Count(y => y >= daysAgo) / max, 0, MidpointRounding.AwayFromZero));
			}

			return dumps
				.Select(x => altMainMap.TryGetValue(x.PlayerId, out var mainId)
					? new RaidDump(x.Timestamp, mainId)
					: x)
				.GroupBy(x => x.PlayerId)
				.ToDictionary(
					x => playerMap[x.Key],
					x => x.Select(y => y.Timestamp).ToHashSet())
				.Select(x => new RaidAttendanceDto
				{
					Name = x.Key.Name,
					Hidden = x.Key.Hidden,
					Admin = x.Key.Admin,
					Rank = x.Key.RankId is null ? "unknown" : rankIdToNameMap[x.Key.RankId.Value],

					_30 = GetPercent(x.Value, thirtyDaysAgo, thirtyDayMaxCount),
					_90 = GetPercent(x.Value, ninetyDaysAgo, ninetyDayMaxCount),
					_180 = GetPercent(x.Value, oneHundredEightyDaysAgo, oneHundredEightDayMaxCount),
				})
				.OrderBy(x => x.Name)
				.ToArray();
		});

		app.MapGet("GetGrantedLootOutput", (LootService lootService, TimeProvider time, bool raidNight) =>
		{
			lootService.EnsureAdminStatus();

			var output = lootService.GetGrantedLootOutput(raidNight);
			var bytes = Encoding.UTF8.GetBytes(output);
			var now = time.GetUtcNow().ToUnixTimeSeconds();

			return Results.File(bytes,
				contentType: "text/plain",
				fileDownloadName: $"RaidLootOutput-{now}.txt");
		});

		app.MapGet("GetPasswords", (LootGodContext db, LootService lootService, TimeProvider time) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var namePasswordsMap = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Alt != true)
				.Where(x => x.Active != false)
				.OrderBy(x => x.Name)
				.Select(x => x.Name + "\t" + "https://raidloot.fly.dev?key=" + x.Key) // TODO:
				.ToArray();
			var data = string.Join(Environment.NewLine, namePasswordsMap);
			var bytes = Encoding.UTF8.GetBytes(data);
			var now = time.GetUtcNow().ToUnixTimeSeconds();

			return Results.File(bytes,
				contentType: "text/plain",
				fileDownloadName: $"GuildPasswords-{now}.txt");
		});
	}
}
