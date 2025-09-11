using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

public static class Extensions
{
	public static void EnsureSingle(this int rows)
	{
		if (rows is not 1)
		{
			throw new Exception("Expected 1 row updated, actual = " + rows);
		}
	}
}

public class Endpoints(string _adminKey)
{
	private static readonly ActivitySource source = new(nameof(Endpoints));
	private readonly byte[] _adminKeyHash = MD5.HashData(Encoding.UTF8.GetBytes(_adminKey));

	static string NormalizeName(string name) => char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();

	void EnsureOwner(string key)
	{
		var hashKey = MD5.HashData(Encoding.UTF8.GetBytes(key));

		if (!Enumerable.SequenceEqual(hashKey, _adminKeyHash))
		{
			throw new UnauthorizedAccessException(key);
		}
	}

	private static bool IsValidDiscordWebhook(string webhook)
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

			// avoid keeping a persistent reference to LootService -> DbContext
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
			db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => dto.RaidNight ? y.RaidDiscordWebhookUrl : y.RotDiscordWebhookUrl, dto.Webhook))
				.EnsureSingle();

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
			var normalizedName = name is null ? "" : NormalizeName(name);

			return db.LootRequests
				.Where(x => x.Player.GuildId == EF.Constant(guildId))
				.Where(x => x.Archived)
				.Where(x => x.AltName!.StartsWith(normalizedName) || x.Player.Name.StartsWith(normalizedName))
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
			var item = new Item(name, Expansion.ToB);
			db.Items.Add(item);
			db.SaveChanges();

			await lootService.RefreshItems();
		});

		app.MapPost("Guest", (LootGodContext db, LootService lootService, MakeGuest dto) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			db.Players
				.Where(x => x.GuildId == guildId && x.Name == dto.Name)
				.ExecuteUpdate(x => x.SetProperty(y => y.Guest, true))
				.EnsureSingle();
		});

		app.MapGet("FreeTrade", (LootGodContext db, LootService lootService) =>
		{
			var guildId = lootService.GetGuildId();
			var guild = db.Guilds.Single(x => x.Id == EF.Constant(guildId));

			return guild.Server is Server.FirionaVie;
		});

		app.MapGet("GetDiscordWebhooks", (LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var guild = db.Guilds.Single(x => x.Id == EF.Constant(guildId));

			return new DiscordWebhooks(guild.RaidDiscordWebhookUrl ?? "", guild.RotDiscordWebhookUrl ?? "");
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

			db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Id == dto.Id)
				.ExecuteUpdate(x => x.SetProperty(y => y.Hidden, y => !y.Hidden))
				.EnsureSingle();
		});

		app.MapPost("TogglePlayerAdmin", (ToggleHiddenAdminPlayer dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();

			db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Id == dto.Id)
				.ExecuteUpdate(x => x.SetProperty(y => y.Admin, y => !y.Admin))
				.EnsureSingle();
		});

		// todo: test
		app.MapPost("ChangePlayerName", (ChangePlayerName dto, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			var normalizedName = NormalizeName(dto.Name);

			// if the "new" player hasn't yet been imported into the system, the logic is simple
			db.Players
				.Where(x => x.Id == dto.Id && x.GuildId == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.Name, y => normalizedName))
				.EnsureSingle();
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
			var player = new Player(NormalizeName(dto.LeaderName), dto.GuildName, dto.Server);
			db.Players.Add(player);
			db.SaveChanges();

			return player.Key!.Value;
		});

		app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, LootLock lootLock) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.LootLocked, lootLock.Enable))
				.EnsureSingle();

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

		app.MapPost("LinkAlt", Results<Ok, BadRequest<string>> (LootGodContext db, LootService lootService, string altName) =>
		{
			var playerId = lootService.GetPlayerId();
			var guildId = lootService.GetGuildId();
			var normalizedAltName = NormalizeName(altName);
			var alt = db.Players
				.Include(x => x.Main)
				.SingleOrDefault(x => x.GuildId == guildId && x.Name == normalizedAltName);

			if (alt is null)
			{
				return TypedResults.BadRequest($"Unable to find guild member with name '{normalizedAltName}'.");
			}
			if (alt.Alt is not true)
			{
				return TypedResults.BadRequest($"'{normalizedAltName}' is not defined as an alt in guild window.");
			}
			if (alt.MainId == playerId)
			{
				return TypedResults.BadRequest($"'{normalizedAltName}' is already linked to you.");
			}
			if (alt.MainId is not null)
			{
				return TypedResults.BadRequest($"'{normalizedAltName}' is already linked to guild member '{alt.Main!.Name}'.");
			}

			alt.MainId = playerId;
			db.SaveChanges();

			return TypedResults.Ok();
		});

		app.MapPost("UnlinkAlt", (LootGodContext db, LootService lootService, string altName) =>
		{
			var playerId = lootService.GetPlayerId();
			var normalizedAltName = NormalizeName(altName);

			db.Players
				.Where(x => x.MainId == playerId && x.Name == normalizedAltName)
				.ExecuteUpdate(x => x.SetProperty(y => y.MainId, (int?)null));
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
			db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.MessageOfTheDay, dto.Message))
				.EnsureSingle();

			await lootService.RefreshMessageOfTheDay(guildId, dto.Message);
		});

		app.MapPost("GrantLootRequest", async (LootGodContext db, LootService lootService, GrantLootRequest dto) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			db.LootRequests
				.Where(x => x.Id == dto.Id)
				.Where(x => x.Player.GuildId == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.Granted, dto.Grant))
				.EnsureSingle();

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
				if (loot is { RaidQuantity: 0, RotQuantity: 0 })
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
			var normalizedName = NormalizeName(dto.Name);
			var newLeader = db.Players.SingleOrDefault(x => x.GuildId == guildId && x.Name == normalizedName);

			if (newLeader is null)
			{
				return TypedResults.BadRequest($"No guild member with name '{normalizedName}' found.");
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

			using var activity = source.StartActivity("ImportDump")?
				.SetTag("FileName", file.FileName)
				.SetTag("FileLength", file.Length)
				.SetTag("Offset", offset);

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
				activity?.SetStatus(ActivityStatusCode.Ok);
				return TypedResults.Ok();
			}
			catch (ImportException ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				return TypedResults.BadRequest(ex.Message);
			}
		}).DisableAntiforgery();

		app.MapGet("GetPlayerAttendance", (LootGodContext db, LootService lootService, TimeProvider time) =>
		{
			var guildId = lootService.GetGuildId();
			var now = time.GetUtcNow();
			var oneHundredEightyDaysAgo = now.AddDays(-180).ToUnixTimeSeconds();
			var ninetyDaysAgo = now.AddDays(-90).ToUnixTimeSeconds();
			var thirtyDaysAgo = now.AddDays(-30).ToUnixTimeSeconds();

			var playerIdToGrantedLootCountMap = db.LootRequests
				.Where(x => x.Player.GuildId == EF.Constant(guildId))
				.Where(x => x.Player.Active != false || x.Player.Guest)
				.Where(x => x.Archived && x.Granted && x.RaidNight)
				.Where(x => x.Item.Expansion == Expansion.ToB)

				// exclude spells from granted count
				.Where(x => !x.Item.Name.EndsWith(" Engram"))

				.GroupBy(x => new
				{
					Id = x.Player.MainId ?? x.PlayerId,
					T2 = x.Item.Name.EndsWith(" of Rebellion"),
				}, (x, y) => KeyValuePair.Create(x, y.Count()))
				.ToDictionary();
			var playerMap = db.Players
				.AsNoTracking()
				.Where(x => x.GuildId == EF.Constant(guildId))
				.Where(x => x.Active != false || x.Guest)
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
				.Where(x => x.Player.Active != false || x.Player.Guest)
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
				.Select(kvp => new RaidAttendanceDto
				{
					Id = kvp.Key.Id,
					Name = kvp.Key.Name,
					Hidden = kvp.Key.Hidden,
					Admin = kvp.Key.Admin,
					Rank = kvp.Key.RankId is null ? "Guest" : rankIdToNameMap[kvp.Key.RankId.Value],
					Class = kvp.Key.Class,
					LastOnDate = kvp.Key.LastOnDate,
					Level = kvp.Key.Level,
					Notes = kvp.Key.Notes,
					Zone = kvp.Key.Zone,
					Alts = playerMap.Values.Where(x => x.MainId == kvp.Key.Id).Select(x => x.Name).ToHashSet(),
					T1GrantedLootCount = playerIdToGrantedLootCountMap.TryGetValue(new { kvp.Key.Id, T2 = false }, out var t1) ? t1 : 0,
					T2GrantedLootCount = playerIdToGrantedLootCountMap.TryGetValue(new { kvp.Key.Id, T2 = true }, out var t2) ? t2 : 0,

					_30 = GetPercent(kvp.Value, thirtyDaysAgo, thirtyDayMaxCount),
					_90 = GetPercent(kvp.Value, ninetyDaysAgo, ninetyDayMaxCount),
					_180 = GetPercent(kvp.Value, oneHundredEightyDaysAgo, oneHundredEightDayMaxCount),
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

			return Results.File(bytes, "text/plain", $"RaidLootOutput-{now}.txt");
		});

		app.MapGet("GetPasswords", (LootGodContext db, LootService lootService, TimeProvider time) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var namePasswordsMap = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Alt != true)
				.Where(x => x.Active != false || x.Guest)
				.OrderBy(x => x.Name)
				.Select(x => x.Name.PadRight(15) + "https://raidloot.fly.dev?key=" + x.Key) // TODO:
				.ToArray();
			var data = string.Join(Environment.NewLine, namePasswordsMap);
			var bytes = Encoding.UTF8.GetBytes(data);
			var now = time.GetUtcNow().ToUnixTimeSeconds();

			return Results.File(bytes, "text/plain", $"GuildPasswords-{now}.txt");
		});
	}
}
