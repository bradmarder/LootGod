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
var source = Environment.GetEnvironmentVariable("DATABASE_URL");
var aspnetcore_urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
using var httpClient = new HttpClient();
using var cts = new CancellationTokenSource();

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
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
//builder.Services.AddOutputCache();
builder.Services.AddSignalR(e => e.EnableDetailedErrors = true);
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

	await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler(opt =>
{
	opt.Run(async context =>
	{
		await Task.CompletedTask;

		var ex = context.Features.Get<IExceptionHandlerFeature>();
		if (ex is not null)
		{
			app.Logger.LogError(ex.Error, nameof(IExceptionHandlerFeature));
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
//if (!app.Environment.IsDevelopment())
//{
//	app.UseSwagger();
//	app.UseSwaggerUI();
//}
app.MapHub<LootHub>("/lootHub");
//app.UseOutputCache();
app.UseMiddleware<LogMiddleware>();
app.MapGet("/test", () => "Hello World!");

//foreach (var item in StaticData.NosLoot)
//{
//	if (!db.Loots.Any(x => x.GuildId == 2 && x.Name == item))
//	{
//		db.Loots.Add(new(item, Expansion.NoS, 2));
//	}
//}

//_ = await db.SaveChangesAsync();

app.MapPost("NewLootNoS", async (LootGodContext db, string name, int guildId) =>
{
	var loot = new Loot(name, Expansion.NoS, guildId) { RaidQuantity = 1 };
	db.Loots.Add(loot);
	await db.SaveChangesAsync();
});

/// TODO: remove guildId parameter, make part of UI
app.MapPost("GuildDiscord", async (LootGodContext db, LootService lootService, string webhook, int guildId) =>
{
	await lootService.EnsureAdminStatus();

	var uri = new Uri(webhook, UriKind.Absolute);
	if (!StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "discordapp.com"))
	{
		throw new Exception(webhook);
	}

	await db.Guilds
		.Where(x => x.Id == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscordWebhookUrl, webhook));
});

app.MapGet("Vacuum", async (LootGodContext db) =>
{
	return await db.Database.ExecuteSqlRawAsync("VACUUM");
});

app.MapGet("GetLootRequests", async (LootService lootService) =>
{
	var guildId = await lootService.GetGuildId();

	return await lootService.LoadLootRequests(guildId);
});

app.MapGet("GetArchivedLootRequests", async (LootGodContext db, LootService lootService, string? name, int? lootId) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	return await db.LootRequests
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
		.ToArrayAsync();
});

app.MapGet("GetLoots", async (LootService lootService) =>
{
	var guildId = await lootService.GetGuildId();

	return await lootService.LoadLoots(guildId);
});

app.MapPost("ToggleHiddenPlayer", async (string playerName, LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Players
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Name == playerName)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Hidden, y => !y.Hidden));
});

app.MapPost("TogglePlayerAdmin", async (string playerName, LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureGuildLeader();

	var guildId = await lootService.GetGuildId();

	await db.Players
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Name == playerName)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Admin, y => !y.Admin));
});

app.MapPost("CreateLootRequest", async (CreateLootRequest dto, LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureRaidLootUnlocked();

	var guildId = await lootService.GetGuildId();
	var playerId = await lootService.GetPlayerId();
	var ip = lootService.GetIPAddress();
	var item = new LootRequest(dto, ip, playerId);
	_ = db.LootRequests.Add(item);
	_ = await db.SaveChangesAsync();

	await lootService.RefreshRequests(guildId);
});

app.MapPost("DeleteLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	await lootService.EnsureRaidLootUnlocked();

	var id = int.Parse(context.Request.Query["id"]!);
	var request = await db.LootRequests.SingleAsync(x => x.Id == id);
	var guildId = await lootService.GetGuildId();
	var playerId = await lootService.GetPlayerId();
	if (request.PlayerId != playerId)
	{
		throw new UnauthorizedAccessException($"PlayerId {playerId} does not have access to loot id {id}");
	}
	if (request.Archived)
	{
		throw new Exception("Cannot delete archived loot requests");
	}

	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteDeleteAsync();

	await lootService.RefreshRequests(guildId);
});

// TODO: this should take lootId
app.MapPost("UpdateLootQuantity", async (CreateLoot dto, LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Loots
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Name == dto.Name)
		.ExecuteUpdateAsync(x => x.SetProperty(y => dto.RaidNight ? y.RaidQuantity : y.RotQuantity, dto.Quantity));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("IncrementLootQuantity", async (LootGodContext db, int id, bool raidNight, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(
			y => raidNight ? y.RaidQuantity : y.RotQuantity,
			y => (raidNight ? y.RaidQuantity : y.RotQuantity) + 1));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("DecrementLootQuantity", async (LootGodContext db, int id, bool raidNight, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(
			y => raidNight ? y.RaidQuantity : y.RotQuantity,
			y => (raidNight ? y.RaidQuantity : y.RotQuantity) == 0 ? 0 : (raidNight ? y.RaidQuantity : y.RotQuantity) - 1));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("CreateGuild", async (LootGodContext db, LootService lootService) =>
{
	// require a guild dump?
	// create single player with admin/leader, single guild, single rank, auto-login with new key, show key to user?
	// require name of guild
	// create tons of new loots
	await db.SaveChangesAsync();
});

// TODO: raid/rot loot locking
app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, bool enable) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	await db.Guilds
		.Where(x => x.Id == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidLootLocked, enable));

	await lootService.RefreshLock(guildId, enable);
});

//app.MapGet("GetPlayerData", async (LootService lootService) =>
//{
//	return null;
//});

app.MapGet("GetLootLock", async (LootService lootService) =>
{
	return await lootService.GetRaidLootLock();
});

app.MapGet("GetPlayerId", async (LootService lootService) =>
{
	return await lootService.GetPlayerId();
});

app.MapGet("GetAdminStatus", async (LootService lootService) =>
{
	return await lootService.GetAdminStatus();
});

app.MapPost("GrantLootRequest", async (LootGodContext db, LootService lootService, int id, bool grant) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	await db.LootRequests
		.Where(x => x.Id == id)
		.Where(x => x.Player.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, grant));

	await lootService.RefreshRequests(guildId);
});

app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService, bool raidNight) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	// capture the output before we archive requests
	var output = await lootService.GetGrantedLootOutput();

	var requests = await db.LootRequests
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => !x.Archived)
		.Where(x => x.RaidNight == raidNight)
		.ToListAsync();
	var loots = await db.Loots
		.Where(x => x.GuildId == guildId)
		.Where(x => (raidNight ? x.RaidQuantity : x.RotQuantity) > 0)
		.ToListAsync();

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

	_ = await db.SaveChangesAsync();

	var guild = await db.Guilds.SingleAsync(x => x.Id == guildId);
	if (guild.DiscordWebhookUrl is not null)
	{
		await lootService.DiscordWebhook(httpClient, output, guild.DiscordWebhookUrl);
	}

	var t1 = lootService.RefreshLoots(guildId);
	var t2 = lootService.RefreshRequests(guildId);
	await Task.WhenAll(t1, t2);
});

app.MapPost("TransferGuildLeadership", async (LootGodContext db, LootService lootService, string name) =>
{
	await lootService.EnsureGuildLeader();

	var guildId = await lootService.GetGuildId();
	var leaderId = await lootService.GetPlayerId();
	var oldLeader = await db.Players.SingleAsync(x => x.Id ==  leaderId);
	var newLeader = await db.Players.SingleAsync(x => x.GuildId == guildId && x.Name == name);

	newLeader.Admin = true;
	newLeader.RankId = oldLeader.RankId;
	oldLeader.RankId = null;

	// should transfering leadership remove admin status?
	// oldLeader.Admin = false;

	await db.SaveChangesAsync();
});

app.MapPost("ImportGuildDump", async (LootGodContext db, LootService lootService, IFormFile file) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

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
	var existingLeader = await db.Players.SingleAsync(x => x.GuildId == guildId && x.Rank!.Name == "Leader");
	if (!dumps.Any(x =>
		StringComparer.OrdinalIgnoreCase.Equals(x.Name, existingLeader.Name)
		&& StringComparer.OrdinalIgnoreCase.Equals(x.Rank, "Leader")))
	{
		return TypedResults.BadRequest("Cannot transfer guild leadership during a dump");
	}

	// create the new ranks
	var existingRankNames = await db.Ranks
		.Where(x => x.GuildId == guildId)
		.Select(x => x.Name)
		.ToListAsync();
	var ranks = dumps
		.Select(x => x.Rank)
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.Except(existingRankNames);
	foreach (var rank in ranks)
	{
		db.Ranks.Add(new(rank, guildId));
	}
	await db.SaveChangesAsync();

	// load all ranks
	var rankNameToIdMap = await db.Ranks
		.Where(x => x.GuildId == guildId)
		.ToDictionaryAsync(x => x.Name, x => x.Id);

	// update existing players
	var players = await db.Players
		.Where(x => x.GuildId == guildId)
		.ToArrayAsync();
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
	await db.SaveChangesAsync();

	// create players who do not exist
	var existingNames = players
		.Select(x => x.Name)
		.ToHashSet();
	var dumpPlayers = dumps
		.Where(x => !existingNames.Contains(x.Name))
		.Select(x => new Player(x, guildId))
		.ToList();
	db.Players.AddRange(dumpPlayers);
	await db.SaveChangesAsync();

	return Results.Ok();
});

app.MapPost("ImportRaidDump", async (LootGodContext db, LootService lootService, IFormFile file, int offset) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

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
	var existingNames = await db.Players
		.Where(x => x.GuildId == guildId)
		.Select(x => x.Name)
		.ToArrayAsync();
	var players = nameToClassMap.Keys
		.Except(existingNames)
		.Select(x => new Player(x, nameToClassMap[x], guildId))
		.ToList();
	db.Players.AddRange(players);
	await db.SaveChangesAsync();

	// save raid dumps for all players
	// example filename = RaidRoster_firiona-20220815-205645.txt
	var parts = file.FileName.Split('-');
	var time = parts[1] + parts[2].Split('.')[0];

	// since the filename of the raid dump doesn't include the timezone, we assume it matches the user's browser UTC offset
	var timestamp = DateTimeOffset
		.ParseExact(time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
		.AddMinutes(offset)
		.ToUnixTimeSeconds();

	var raidDumps = (await db.Players
		.Where(x => x.GuildId == guildId)
		.Where(x => nameToClassMap.Keys.Contains(x.Name)) // ContainsKey cannot be translated by EFCore
		.Select(x => x.Id)
		.ToArrayAsync())
		.Select(x => new RaidDump(timestamp, x))
		.ToArray();
	db.RaidDumps.AddRange(raidDumps);

	// A unique constraint on the composite index for (Timestamp/Player) will cause exceptions for duplicate raid dumps.
	// It is safe/intended to ignore these exceptions for idempotency.
	try
	{
		await db.SaveChangesAsync();
	}
	catch (DbUpdateException) { }
});

app.MapPost("BulkImportRaidDump", async (LootGodContext db, LootService lootService, IFormFile file, int offset) =>
{
	await lootService.EnsureAdminStatus();

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
		var port = aspnetcore_urls!.Split(':').Last();
		var res = await httpClient.PostAsync($"http://{IPAddress.Loopback}:{port}/ImportRaidDump?offset={offset}", form);
		res.EnsureSuccessStatusCode();
	}
});

app.MapGet("GetPlayerAttendance", async (LootGodContext db, LootService lootService) =>
{
	var guildId = await lootService.GetGuildId();
	var oneHundredEightyDaysAgo = DateTimeOffset.UtcNow.AddDays(-180).ToUnixTimeSeconds();
	var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
	var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
	var ninety = DateOnly.FromDateTime(ninetyDaysAgo);
	var thirty = DateOnly.FromDateTime(thirtyDaysAgo);

	var playerMap = await db.Players
		.Where(x => x.GuildId == guildId)
		.ToDictionaryAsync(x => x.Id, x => (x.Name, x.RankId, x.Hidden, x.Admin));
	var rankIdToNameMap = await db.Ranks
		.Where(x => x.GuildId == guildId)
		.ToDictionaryAsync(x => x.Id, x => x.Name);
	var dumps = await db.RaidDumps
		.AsNoTracking()
		.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Player.Active == true)
		.ToListAsync();
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

app.MapGet("GetGrantedLootOutput", async (LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var output = await lootService.GetGrantedLootOutput();
	var bytes = Encoding.UTF8.GetBytes(output);

	return Results.File(bytes,
		contentType: "text/plain",
		fileDownloadName: "RaidLootOutput-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".txt");
});

app.MapGet("/GetPasswords", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureGuildLeader();

	var guildId = await lootService.GetGuildId();
	var namePasswordsMap = await db.Players
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Alt != true)
		.Where(x => x.Active != false)
		.OrderBy(x => x.Name)
		.Select(x => x.Name + " " + "https://raidloot.fly.dev?key=" + x.Key)
		.ToArrayAsync();
	var data = string.Join(Environment.NewLine, namePasswordsMap);
	var bytes = Encoding.UTF8.GetBytes(data);

	return Results.File(bytes,
		contentType: "text/plain",
		fileDownloadName: "GuildPasswords-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".txt");
});

await app.RunAsync(cts.Token);

public record CreateLoot(byte Quantity, string Name, bool RaidNight);