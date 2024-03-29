﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace LootGod;

public record Endpoints(string _adminKey, string _backup)
{
	public record CreateLoot(byte Quantity, int ItemId, bool RaidNight);
	public record CreateGuild(string LeaderName, string GuildName, Server Server);

	private readonly HttpClient _httpClient = new();

	void EnsureOwner(string key)
	{
		if (key != _adminKey)
		{
			throw new UnauthorizedAccessException(key);
		}
	}

	public void Map(IEndpointRouteBuilder app)
	{
		app.MapGet("SSE", async (HttpContext ctx, LootService service) =>
		{
			var token = ctx.RequestAborted;
			var connectionId = ctx.Connection.Id;

			ctx.Response.Headers.Append("Content-Type", "text/event-stream");
			ctx.Response.Headers.Append("Cache-Control", "no-cache");
			ctx.Response.Headers.Append("Connection", "keep-alive");

			await ctx.Response.WriteAsync($"data: empty\n\n\n", token);
			await ctx.Response.Body.FlushAsync(token);

			service.AddDataSink(connectionId, ctx.Response);
			token.Register(() => service.RemoveDataSink(connectionId));

			await Task.Delay(Timeout.InfiniteTimeSpan, token);
		});

		app.MapPost("GuildDiscord", (LootGodContext db, LootService lootService, string webhook, bool raidNight) =>
		{
			lootService.EnsureGuildLeader();

			if (!string.IsNullOrEmpty(webhook))
			{
				var uri = new Uri(webhook, UriKind.Absolute);
				if (uri.Host != "discord.com"
					|| uri.Segments[0] != "/"
					|| uri.Segments[1] != "api/"
					|| uri.Segments[2] != "webhooks/"
					|| !long.TryParse(uri.Segments[3].TrimEnd('/'), out _))
				{
					throw new Exception(webhook);
				}
			}
			var guildId = lootService.GetGuildId();
			db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => raidNight ? y.RaidDiscordWebhookUrl : y.RotDiscordWebhookUrl, webhook));
		});

		app.MapGet("Vacuum", (LootGodContext db, string key) =>
		{
			EnsureOwner(key);
			return db.Database.ExecuteSqlRaw("VACUUM");
		});

		app.MapGet("Backup", (LootGodContext db, string key) =>
		{
			EnsureOwner(key);

			db.Database.ExecuteSqlRaw("VACUUM INTO {0}", _backup);

			var stream = File.OpenRead(_backup);
			var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var name = $"backup-{now}.db";

			return Results.Stream(stream, fileDownloadName: name);
		});

		app.MapGet("DeleteBackup", (string key) =>
		{
			EnsureOwner(key);
			File.Delete(_backup);
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
				.OrderByDescending(x => x.Spell != null)
				.ThenBy(x => x.ItemId)
				.ThenByDescending(x => x.AltName ?? x.Player.Name)
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
		app.MapPost("CreateLoot", async (LootGodContext db, string name, LootService lootService) =>
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

			return db.Guilds
				.Where(x => x.Id == guildId)
				.Select(x => new
				{
					Raid = x.RaidDiscordWebhookUrl ?? "",
					Rot = x.RotDiscordWebhookUrl ?? "",
				})
				.Single();
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

		app.MapPost("ToggleHiddenPlayer", (string playerName, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Name == playerName)
				.ExecuteUpdate(x => x.SetProperty(y => y.Hidden, y => !y.Hidden));
		});

		app.MapPost("TogglePlayerAdmin", (string playerName, LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();

			db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Name == playerName)
				.ExecuteUpdate(x => x.SetProperty(y => y.Admin, y => !y.Admin));
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
			_ = db.LootRequests.Add(request);
			_ = db.SaveChanges();

			await lootService.RefreshRequests(guildId);
		});

		app.MapPost("DeleteLootRequest", async (LootGodContext db, int id, LootService lootService) =>
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
				loot = new Loot { GuildId = guildId, ItemId = dto.ItemId };
				db.Loots.Add(loot);
			}
			_ = dto.RaidNight
				? loot.RaidQuantity = dto.Quantity
				: loot.RotQuantity = dto.Quantity;
			db.SaveChanges();

			await lootService.RefreshLoots(guildId);
		});

		app.MapPost("IncrementLootQuantity", async (LootGodContext db, int itemId, bool raidNight, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			var loot = db.Loots.Single(x => x.GuildId == guildId && x.ItemId == itemId);
			_ = raidNight
				? loot.RaidQuantity++
				: loot.RotQuantity++;

			db.SaveChanges();

			await lootService.RefreshLoots(guildId);
		});

		app.MapPost("DecrementLootQuantity", async (LootGodContext db, int itemId, bool raidNight, LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			var loot = db.Loots.Single(x => x.GuildId == guildId && x.ItemId == itemId);
			_ = raidNight
				? loot.RaidQuantity--
				: loot.RotQuantity--;

			// if quantities are zero, then remove the loot record
			if (loot.RaidQuantity == 0 && loot.RotQuantity == 0)
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

		app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, bool enable) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			db.Guilds
				.Where(x => x.Id == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.LootLocked, enable));

			await lootService.RefreshLock(guildId, enable);
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

			return db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Alt == true)
				.Where(x => x.MainId == null)
				.Where(x => x.Name == validAltName)
				.ExecuteUpdate(x => x.SetProperty(y => y.MainId, playerId));
		});

		app.MapGet("GetLootLock", (LootService lootService) =>
		{
			return lootService.GetRaidLootLock();
		});

		app.MapGet("GetPlayerId", (LootService lootService) =>
		{
			return lootService.GetPlayerId();
		});

		app.MapGet("GetAdminStatus", (LootService lootService) =>
		{
			return lootService.GetAdminStatus();
		});

		app.MapGet("GetLeaderStatus", (LootService lootService) =>
		{
			return lootService.IsGuildLeader();
		});

		app.MapPost("GrantLootRequest", async (LootGodContext db, LootService lootService, int id, bool grant) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();
			db.LootRequests
				.Where(x => x.Id == id)
				.Where(x => x.Player.GuildId == guildId)
				.ExecuteUpdate(x => x.SetProperty(y => y.Granted, grant));

			await lootService.RefreshRequests(guildId);
		});

		app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService, bool raidNight) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			// capture the output before we archive requests
			var output = lootService.GetGrantedLootOutput();

			var requests = db.LootRequests
				.Where(x => x.Player.GuildId == guildId)
				.Where(x => !x.Archived)
				.Where(x => x.RaidNight == raidNight)
				.ToList();
			var loots = db.Loots
				.Where(x => x.GuildId == guildId)
				.Where(x => (raidNight ? x.RaidQuantity : x.RotQuantity) > 0)
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

				if (raidNight)
				{
					loot.RotQuantity += (byte)(loot.RaidQuantity - grantedQuantity);
					loot.RaidQuantity = 0;
				}
				else
				{
					loot.RotQuantity -= (byte)grantedQuantity;
				}

				// if quantities are zero, then remove the loot record
				if (loot.RaidQuantity == 0 && loot.RotQuantity == 0)
				{
					db.Loots.Remove(loot);
				}
			}

			_ = db.SaveChanges();

			var guild = db.Guilds.Single(x => x.Id == guildId);
			var webhook = raidNight ? guild.RaidDiscordWebhookUrl : guild.RotDiscordWebhookUrl;
			if (webhook is not null)
			{
				await lootService.DiscordWebhook(_httpClient, output, webhook);
			}

			var t1 = lootService.RefreshLoots(guildId);
			var t2 = lootService.RefreshRequests(guildId);
			await Task.WhenAll(t1, t2);
		});

		app.MapPost("TransferGuildLeadership", (LootGodContext db, LootService lootService, string name) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var leaderId = lootService.GetPlayerId();
			var oldLeader = db.Players.Single(x => x.Id == leaderId);
			var newLeader = db.Players.Single(x => x.GuildId == guildId && x.Name == name);

			if (oldLeader.Id == newLeader.Id) { throw new Exception("cannot transfer leadership to self?!"); }

			newLeader.Admin = true;
			newLeader.RankId = oldLeader.RankId;
			oldLeader.RankId = null;

			// should transfering leadership remove admin status?
			// oldLeader.Admin = false;

			db.SaveChanges();
		});

		app.MapPost("ImportGuildDump", async (LootGodContext db, LootService lootService, IFormFile file) =>
		{
			lootService.EnsureAdminStatus();

			var guildId = lootService.GetGuildId();

			// parse the guild dump player output
			await using var stream = file.OpenReadStream();
			using var sr = new StreamReader(stream);
			var output = await sr.ReadToEndAsync();
			var dumps = output
				.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Split('\t'))
				.Select(x => new GuildDumpPlayerOutput(x))
				.ToArray();

			// ensure not partial guild dump by checking a leader exists
			if (!dumps.Any(x => StringComparer.OrdinalIgnoreCase.Equals("Leader", x.Rank)))
			{
				return TypedResults.BadRequest("Partial Guild Dump - Missing Leader Rank");
			}

			// ensure guild leader does not change (must use TransferGuildLeadership endpoint instead)
			var existingLeader = db.Players.Single(x => x.GuildId == guildId && x.Rank!.Name == "Leader");
			if (!dumps.Any(x =>
				StringComparer.OrdinalIgnoreCase.Equals(x.Name, existingLeader.Name)
				&& StringComparer.OrdinalIgnoreCase.Equals(x.Rank, "Leader")))
			{
				return TypedResults.BadRequest("Cannot transfer guild leadership during a dump");
			}

			// create the new ranks
			var existingRankNames = db.Ranks
				.Where(x => x.GuildId == guildId)
				.Select(x => x.Name)
				.ToList();
			var ranks = dumps
				.Select(x => x.Rank)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Except(existingRankNames);
			foreach (var rank in ranks)
			{
				db.Ranks.Add(new(rank, guildId));
			}
			db.SaveChanges();

			// load all ranks
			var rankNameToIdMap = db.Ranks
				.Where(x => x.GuildId == guildId)
				.ToDictionary(x => x.Name, x => x.Id);

			// update existing players
			var players = db.Players
				.Where(x => x.GuildId == guildId)
				.ToArray();
			foreach (var player in players)
			{
				var dump = dumps.SingleOrDefault(x => x.Name == player.Name);
				if (dump is not null)
				{
					player.Active = true;
					player.RankId = rankNameToIdMap[dump.Rank];
					player.LastOnDate = dump.LastOnDate;
					player.Level = dump.Level;
					player.Alt = dump.Alt;
					player.Notes = dump.Notes;

					// TODO: shouldn't be necessary, bug with Kyoto class defaulting to Bard
					player.Class = Player._classNameToEnumMap[dump.Class];
				}
				else
				{
					// if a player no longer appears in a guild dump output, we assert them inactive
					// TODO: disconnect removed player/connection from hub
					player.Active = false;
					player.Admin = false;
				}
			}
			db.SaveChanges();

			// create players who do not exist
			var existingNames = players
				.Select(x => x.Name)
				.ToHashSet();
			var dumpPlayers = dumps
				.Where(x => !existingNames.Contains(x.Name))
				.Select(x => new Player(x, guildId))
				.ToList();
			db.Players.AddRange(dumpPlayers);
			db.SaveChanges();

			return Results.Ok();
		}).DisableAntiforgery();

		app.MapPost("ImportRaidDump", async (LootService lootService, IFormFile file, int offset) =>
		{
			lootService.EnsureAdminStatus();

			await using var stream = file.OpenReadStream();
			await lootService.ImportRaidDump(stream, file.FileName, offset);

		}).DisableAntiforgery();

		app.MapPost("BulkImportRaidDump", async (LootService lootService, IFormFile file, int offset) =>
		{
			lootService.EnsureAdminStatus();

			await using var stream = file.OpenReadStream();
			using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

			foreach (var entry in zip.Entries.OrderBy(x => x.LastWriteTime))
			{
				await using var dump = entry.Open();
				await lootService.ImportRaidDump(dump, entry.FullName, offset);
			}
		}).DisableAntiforgery();

		app.MapGet("GetPlayerAttendance", (LootGodContext db, LootService lootService) =>
		{
			var guildId = lootService.GetGuildId();
			var oneHundredEightyDaysAgo = DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeSeconds();
			var ninety = DateTime.UtcNow.AddDays(-90);
			var thirty = DateTime.UtcNow.AddDays(-30);
			var ninetyDaysAgo = DateOnly.FromDateTime(ninety);
			var thirtyDaysAgo = DateOnly.FromDateTime(thirty);

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
				.Select(x => DateTimeOffset.FromUnixTimeSeconds(x.Timestamp))
				.Select(x => DateOnly.FromDateTime(x.DateTime))
				.ToHashSet();
			var oneHundredEightDayMaxCount = uniqueDates.Count;
			var ninetyDayMaxCount = uniqueDates.Count(x => x > ninetyDaysAgo);
			var thirtyDayMaxCount = uniqueDates.Count(x => x > thirtyDaysAgo);

			var raidPlayers = dumps
				.Select(x => altMainMap.TryGetValue(x.PlayerId, out var mainId)
					? new RaidDump(x.Timestamp, mainId)
					: x)
				.GroupBy(x => x.PlayerId)
				.ToDictionary(
					x => playerMap[x.Key],
					x => x
						.Select(y => DateTimeOffset.FromUnixTimeSeconds(y.Timestamp))
						.Select(y => DateOnly.FromDateTime(y.DateTime))
						.ToHashSet());

			// if there are zero raid dumps for mains, include them in RA
			//playerMap
			//	.Select(x => x.Value)
			//	.Where(x => x.MainId is null)
			//	.Where(x => x.Alt != true)
			//	.ToList()
			//	.ForEach(x => raidPlayers.TryAdd(x, new()));

			return raidPlayers
				.Select(x => new RaidAttendanceDto
				{
					Name = x.Key.Name,
					Hidden = x.Key.Hidden,
					Admin = x.Key.Admin,
					Rank = x.Key.RankId is null ? "unknown" : rankIdToNameMap[x.Key.RankId.Value],

					_30 = (byte)(thirtyDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count(y => y > thirtyDaysAgo) / thirtyDayMaxCount, 0, MidpointRounding.AwayFromZero)),
					_90 = (byte)(ninetyDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count(y => y > ninetyDaysAgo) / ninetyDayMaxCount, 0, MidpointRounding.AwayFromZero)),
					_180 = (byte)(oneHundredEightDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count / oneHundredEightDayMaxCount, 0, MidpointRounding.AwayFromZero)),
				})
				.OrderBy(x => x.Name)
				.ToArray();
		});

		app.MapGet("GetGrantedLootOutput", (LootService lootService) =>
		{
			lootService.EnsureAdminStatus();

			var output = lootService.GetGrantedLootOutput();
			var bytes = Encoding.UTF8.GetBytes(output);
			var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			return Results.File(bytes,
				contentType: "text/plain",
				fileDownloadName: $"RaidLootOutput-{now}.txt");
		});

		app.MapGet("GetPasswords", (LootGodContext db, LootService lootService) =>
		{
			lootService.EnsureGuildLeader();

			var guildId = lootService.GetGuildId();
			var namePasswordsMap = db.Players
				.Where(x => x.GuildId == guildId)
				.Where(x => x.Alt != true)
				.Where(x => x.Active != false)
				.OrderBy(x => x.Name)
				.Select(x => x.Name + "\t" + "https://raidloot.fly.dev?key=" + x.Key)
				.ToArray();
			var data = string.Join(Environment.NewLine, namePasswordsMap);
			var bytes = Encoding.UTF8.GetBytes(data);
			var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			return Results.File(bytes,
				contentType: "text/plain",
				fileDownloadName: $"GuildPasswords-{now}.txt");
		});
	}
}
