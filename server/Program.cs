using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var adminKey = Environment.GetEnvironmentVariable("ADMIN_KEY")!;
var backup = Environment.GetEnvironmentVariable("BACKUP_URL")!;
var source = Environment.GetEnvironmentVariable("DATABASE_URL")!;
var aspnetcore_urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")!;
using var httpClient = new HttpClient();
using var cts = new CancellationTokenSource();

void EnsureOwner(string key)
{
	if (key != adminKey)
	{
		throw new UnauthorizedAccessException(key);
	}
}

Console.CancelKeyPress += (o, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

var useInMemoryDatabase = source is null;
var connString = useInMemoryDatabase
	? new SqliteConnectionStringBuilder
	{
		DataSource = ":memory:",
		Cache = SqliteCacheMode.Shared,
		Mode = SqliteOpenMode.Memory,
	}
	: new SqliteConnectionStringBuilder { DataSource = source };

// required to keep in-memory database alive
using var conn = new SqliteConnection(connString.ConnectionString);
if (useInMemoryDatabase)
{
	conn.Open();
}
else
{
	conn.Dispose();
}

builder.Services.AddDbContextPool<LootGodContext>(x => x.UseSqlite(connString.ConnectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddScoped<LootService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);
var logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console(outputTemplate: "[{Timestamp:u} {Level:u3}] {Message:lj} " + "{Properties:j}{NewLine}{Exception}")
	.MinimumLevel.Override(nameof(Microsoft), LogEventLevel.Warning)
	.MinimumLevel.Override(nameof(System), LogEventLevel.Warning)
	.CreateLogger();
builder.Services.AddLogging(x => x
	.ClearProviders()
	.AddSerilog(logger)
	.Configure(y => y.ActivityTrackingOptions = ActivityTrackingOptions.None)
);

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();
}

app.UseExceptionHandler(opt =>
{
	opt.Run(async context =>
	{
		await Task.CompletedTask;

		var ex = context.Features.Get<IExceptionHandlerFeature>();
		if (ex is not null)
		{
			app.Logger.LogError(ex.Error, context.Request.Path);
		}
	});
});
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
	OnPrepareResponse = x =>
	{
		x.Context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none';";
		x.Context.Response.Headers.XFrameOptions = "DENY";
		x.Context.Response.Headers.Add("Referrer-Policy", "no-referrer");
	}
});
app.MapHub<LootHub>("/ws/lootHub");
app.UsePathBase("/api");
app.UseMiddleware<LogMiddleware>();
app.MapGet("test", () => "Hello World!");

app.MapPost("GuildDiscord", (LootGodContext db, LootService lootService, string webhook) =>
{
	lootService.EnsureAdminStatus();

	var uri = new Uri(webhook, UriKind.Absolute);
	if (!StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "discordapp.com"))
	{
		throw new Exception(webhook);
	}

	var guildId = lootService.GetGuildId();
	db.Guilds
		.Where(x => x.Id == guildId)
		.ExecuteUpdate(x => x.SetProperty(y => y.DiscordWebhookUrl, webhook));
});

app.MapGet("Vacuum", (LootGodContext db, string key) =>
{
	EnsureOwner(key);
	return db.Database.ExecuteSqlRaw("VACUUM");
});

app.MapGet("Backup", (LootGodContext db, string key) =>
{
	EnsureOwner(key);
	db.Database.ExecuteSqlRaw($"VACUUM INTO '{backup}'");
	var stream = File.OpenRead(backup);
	var name = $"backup-{DateTimeOffset.UnixEpoch.ToUnixTimeSeconds()}.db";
	return Results.Stream(stream, fileDownloadName: name);
});

app.MapGet("DeleteBackup", (string key) =>
{
	EnsureOwner(key);
	File.Delete(backup);
});

app.MapGet("GetLootRequests", (LootService lootService) =>
{
	var guildId = lootService.GetGuildId();

	return lootService.LoadLootRequests(guildId);
});

app.MapGet("GetArchivedLootRequests", (LootGodContext db, LootService lootService, string? name, int? lootId) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();

	return db.LootRequests
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Archived)
		.Where(x => name == null || x.AltName!.StartsWith(name) || x.Player.Name.StartsWith(name))
		.Where(x => lootId == null || x.LootId == lootId)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
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
			LootId = x.LootId,
			Quantity = x.Quantity,
			RaidNight = x.RaidNight,
			IsAlt = x.IsAlt,
			Granted = x.Granted,
			CurrentItem = x.CurrentItem,
		})
		.ToArray();
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
	var item = new LootRequest(dto, ip, playerId);
	_ = db.LootRequests.Add(item);
	_ = db.SaveChanges();

	await lootService.RefreshRequests(guildId);
});

app.MapPost("DeleteLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	lootService.EnsureRaidLootUnlocked();

	var id = int.Parse(context.Request.Query["id"]!);
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

	db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteDelete();

	await lootService.RefreshRequests(guildId);
});

// TODO: this should take lootId
app.MapPost("UpdateLootQuantity", async (CreateLoot dto, LootGodContext db, LootService lootService) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();

	db.Loots
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Name == dto.Name)
		.ExecuteUpdate(x => x.SetProperty(y => dto.RaidNight ? y.RaidQuantity : y.RotQuantity, dto.Quantity));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("IncrementLootQuantity", async (LootGodContext db, int id, bool raidNight, LootService lootService) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();

	db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdate(x => x.SetProperty(
			y => raidNight ? y.RaidQuantity : y.RotQuantity,
			y => (raidNight ? y.RaidQuantity : y.RotQuantity) + 1));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("DecrementLootQuantity", async (LootGodContext db, int id, bool raidNight, LootService lootService) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();

	db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdate(x => x.SetProperty(
			y => raidNight ? y.RaidQuantity : y.RotQuantity,
			y => (raidNight ? y.RaidQuantity : y.RotQuantity) == 0 ? 0 : (raidNight ? y.RaidQuantity : y.RotQuantity) - 1));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("CreateGuild", (LootGodContext db, string leaderName, string guildName) =>
{
	var lootTemplate = db.Loots
		.AsNoTracking()
		.Where(x => x.GuildId == 1)
		.ToArray();
	var player = new Player(leaderName, guildName);
	var loots = lootTemplate.Select(x => new Loot(x.Name, x.Expansion, player.Guild));
	db.Loots.AddRange(loots);
	db.Players.Add(player);
	db.SaveChanges();

	// TODO: connect with signalR
	return player.Key;
});

// TODO: raid/rot loot locking
app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, bool enable) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();
	db.Guilds
		.Where(x => x.Id == guildId)
		.ExecuteUpdate(x => x.SetProperty(y => y.LootLocked, enable));

	await lootService.RefreshLock(guildId, enable);
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
			.Where(x => x.LootId == loot.Id && x.Granted)
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
	}

	_ = db.SaveChanges();

	var guild = db.Guilds.Single(x => x.Id == guildId);
	if (guild.DiscordWebhookUrl is not null)
	{
		await lootService.DiscordWebhook(httpClient, output, guild.DiscordWebhookUrl);
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
	var oldLeader = db.Players.Single(x => x.Id ==  leaderId);
	var newLeader = db.Players.Single(x => x.GuildId == guildId && x.Name == name);

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
});

app.MapPost("ImportRaidDump", async (LootGodContext db, LootService lootService, IFormFile file, int offset) =>
{
	lootService.EnsureAdminStatus();

	var guildId = lootService.GetGuildId();

	// read the raid dump output file
	await using var stream = file.OpenReadStream();
	using var sr = new StreamReader(stream);
	var output = await sr.ReadToEndAsync();
	var nameToClassMap = output
		.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
		.Select(x => x.Split('\t'))
		.Where(x => x.Length > 4) // filter out "missing" rows that start with a number, but have nothing after
		.ToDictionary(x => x[1], x => x[3]);

	// create players who do not exist
	var existingNames = db.Players
		.Where(x => x.GuildId == guildId)
		.Select(x => x.Name)
		.ToArray();
	var players = nameToClassMap.Keys
		.Except(existingNames)
		.Select(x => new Player(x, nameToClassMap[x], guildId))
		.ToList();
	db.Players.AddRange(players);
	db.SaveChanges();

	// save raid dumps for all players
	// example filename = RaidRoster_firiona-20220815-205645.txt
	var parts = file.FileName.Split('-');
	var time = parts[1] + parts[2].Split('.')[0];

	// since the filename of the raid dump doesn't include the timezone, we assume it matches the user's browser UTC offset
	var timestamp = DateTimeOffset
		.ParseExact(time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
		.AddMinutes(offset)
		.ToUnixTimeSeconds();

	var raidDumps = db.Players
		.Where(x => x.GuildId == guildId)
		.Where(x => nameToClassMap.Keys.Contains(x.Name)) // ContainsKey cannot be translated by EFCore
		.Select(x => x.Id)
		.ToArray()
		.Select(x => new RaidDump(timestamp, x))
		.ToArray();
	db.RaidDumps.AddRange(raidDumps);

	// A unique constraint on the composite index for (Timestamp/Player) will cause exceptions for duplicate raid dumps.
	// It is safe/intended to ignore these exceptions for idempotency.
	try
	{
		db.SaveChanges();
	}
	catch (DbUpdateException) { }
});

app.MapPost("BulkImportRaidDump", async (LootGodContext db, LootService lootService, IFormFile file, int offset) =>
{
	lootService.EnsureAdminStatus();

	var playerKey = lootService.GetPlayerKey();

	await using var stream = file.OpenReadStream();
	using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

	foreach (var entry in zip.Entries.OrderBy(x => x.LastWriteTime))
	{
		await using var dump = entry.Open();
		using var sr = new StreamReader(dump);
		var data = await sr.ReadToEndAsync();
		using var content = new StringContent(data);
		using var form = new MultipartFormDataContent
		{
			{ content, "file", entry.FullName }
		};
		form.Headers.Add("Player-Key", playerKey!.ToString());
		var port = aspnetcore_urls.Split(':').Last();
		var res = await httpClient.PostAsync($"http://{IPAddress.Loopback}:{port}/ImportRaidDump?offset={offset}", form);
		res.EnsureSuccessStatusCode();
	}
});

app.MapGet("GetPlayerAttendance", (LootGodContext db, LootService lootService) =>
{
	var guildId = lootService.GetGuildId();
	var oneHundredEightyDaysAgo = DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeSeconds();
	var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
	var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
	var ninety = DateOnly.FromDateTime(ninetyDaysAgo);
	var thirty = DateOnly.FromDateTime(thirtyDaysAgo);

	var playerMap = db.Players
		.Where(x => x.GuildId == guildId)
		.ToDictionary(x => x.Id, x => (x.Name, x.RankId, x.Hidden, x.Admin));
	var rankIdToNameMap = db.Ranks
		.Where(x => x.GuildId == guildId)
		.ToDictionary(x => x.Id, x => x.Name);
	var dumps = db.RaidDumps
		.AsNoTracking()
		.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Player.Active == true)
		.ToList();
	var uniqueDates = dumps
		.Select(x => DateTimeOffset.FromUnixTimeSeconds(x.Timestamp))
		.Select(x => DateOnly.FromDateTime(x.DateTime))
		.ToHashSet();
	var oneHundredEightDayMaxCount = uniqueDates.Count;
	var ninetyDayMaxCount = uniqueDates.Count(x => x > ninety);
	var thirtyDayMaxCount = uniqueDates.Count(x => x > thirty);

	return dumps
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

			_30 = (byte)(thirtyDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count(y => y > thirty) / thirtyDayMaxCount, 0, MidpointRounding.AwayFromZero)),
			_90 = (byte)(ninetyDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count(y => y > ninety) / ninetyDayMaxCount, 0, MidpointRounding.AwayFromZero)),
			_180 = (byte)(oneHundredEightDayMaxCount == 0 ? 0 : Math.Round(100d * x.Value.Count() / oneHundredEightDayMaxCount, 0, MidpointRounding.AwayFromZero)),
		})
		.OrderBy(x => x.Name)
		.ToArray();
});

app.MapGet("GetGrantedLootOutput", (LootService lootService) =>
{
	lootService.EnsureAdminStatus();

	var output = lootService.GetGrantedLootOutput();
	var bytes = Encoding.UTF8.GetBytes(output);

	return Results.File(bytes,
		contentType: "text/plain",
		fileDownloadName: "RaidLootOutput-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".txt");
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
		.Select(x => x.Name + " " + "https://raidloot.fly.dev?key=" + x.Key)
		.ToArray();
	var data = string.Join(Environment.NewLine, namePasswordsMap);
	var bytes = Encoding.UTF8.GetBytes(data);

	return Results.File(bytes,
		contentType: "text/plain",
		fileDownloadName: "GuildPasswords-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".txt");
});

await app.RunAsync(cts.Token);

public record CreateLoot(byte Quantity, string Name, bool RaidNight);