using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var source = Environment.GetEnvironmentVariable("DATABASE_URL");
var rotLootUrl = Environment.GetEnvironmentVariable("ROT_LOOT_URL");
var aspnetcore_urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var password = Environment.GetEnvironmentVariable("PASSWORD");
using var httpClient = new HttpClient();

var connString = new SqliteConnectionStringBuilder { DataSource = source };
builder.Services.AddDbContextPool<LootGodContext>(x => x.UseSqlite(connString.ConnectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddCors(options =>
		options.AddDefaultPolicy(policy =>
			policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetIsOriginAllowed(x => true)));
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddOutputCache();
builder.Services.AddSignalR(e => e.EnableDetailedErrors = true);
builder.Services.AddScoped<LootService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);

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
		var ex = context.Features.Get<IExceptionHandlerFeature>();
		if (ex is not null)
		{
			var err = $"Error: {ex.Error.Message} {ex.Error.StackTrace} {ex.Error.InnerException?.Message}";
			await context.Response.WriteAsync(err);
			await Console.Out.WriteAsync(err);
		}
	});
});
app.UseResponseCompression();
app.UseDefaultFiles();
if (app.Environment.IsProduction())
{
	app.UseStaticFiles(new StaticFileOptions
	{
		OnPrepareResponse = x => x.Context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none';"
	});
}
if (app.Environment.IsDevelopment())
{
	app.UseCors();
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<LootHub>("/lootHub");
app.UseOutputCache();
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
app.MapPost("GuildDiscord", async (LootGodContext db, string webhook, int guildId) =>
{
	var uri = new Uri(webhook, UriKind.Absolute);
	if (!StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "discordapp.com"))
	{
		throw new Exception(webhook);
	}
	var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == guildId);
	guild!.DiscordWebhookUrl = webhook;
	await db.SaveChangesAsync();
});

app.MapGet("GetLootRequests", async (LootGodContext db, LootService lootService) =>
{
	var guildId = await lootService.GetGuildId();

	return (await db.LootRequests
		.AsNoTracking()
		.Include(x => x.Player)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => !x.Archived)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.AltName ?? x.Player.Name)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetArchivedLootRequests", async (LootGodContext db, LootService lootService, string? name, int? lootId) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	return (await db.LootRequests
		.AsNoTracking()
		.Include(x => x.Player)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Archived)
		.Where(x => name == null || EF.Functions.Like(x.AltName!, name) || EF.Functions.Like(x.Player.Name, name))
		.Where(x => lootId == null || x.LootId == lootId)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.AltName ?? x.Player.Name)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetLoots", async (LootGodContext db, LootService lootService) =>
{
	var guildId = await lootService.GetGuildId();

	return (await db.Loots
		.AsNoTracking()
		.Where(x => x.GuildId == guildId)
		.Where(x => x.Expansion == Expansion.ToL || x.Expansion == Expansion.NoS)
		.OrderBy(x => x.Name)
		.ToListAsync())
		.Select(x => new LootDto(x));
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
	var request = await db.LootRequests.FirstOrDefaultAsync(x => x.Id == id);
	var guildId = await lootService.GetGuildId();
	var playerId = await lootService.GetPlayerId();
	if (request?.PlayerId != playerId)
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
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, dto.Quantity));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("IncrementLootQuantity", async (LootGodContext db, int id, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, y => y.RaidQuantity + 1));

	await lootService.RefreshLoots(guildId);
});

app.MapPost("DecrementLootQuantity", async (LootGodContext db, int id, LootService lootService) =>
{
	//var unarchivedGrantedLootRequestCount = await db.LootRequests.CountAsync(x => !x.Archived && x.Granted && x.LootId == id);
	//var loot = await db.Loots.SingleAsync(x => x.Id == id);

	//// user would need to un-grant loot requests before decrementing quantity
	//if (loot.Quantity == unarchivedGrantedLootRequestCount)
	//{
	//	return;
	//}
	// TODO: RotQuantity

	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	await db.Loots
		.Where(x => x.Id == id)
		.Where(x => x.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, y => y.RaidQuantity == 0 ? 0 : y.RaidQuantity - 1));

	await lootService.RefreshLoots(guildId);
});

// TODO: raid/rot loot locking
app.MapPost("ToggleLootLock", async (LootGodContext db, LootService lootService, bool enable) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == guildId);
	guild!.RaidLootLocked = enable;
	_ = await db.SaveChangesAsync();

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

app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	// capture the output before we archive requests
	var output = await lootService.GetGrantedLootOutput();

	var items = await db.LootRequests
		.Include(x => x.Loot)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => !x.Archived)
		.ToListAsync();

	foreach (var x in items)
	{
		x.Archived = true;
	}
	foreach (var x in items.Where(x => x.Granted))
	{
		x.Loot.RaidQuantity -= x.Quantity;
	}

	_ = await db.SaveChangesAsync();

	var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == guildId);
	if (guild!.DiscordWebhookUrl is not null)
	{
		try
		{
			var json = new { content = $"```{Environment.NewLine}{output}{Environment.NewLine}```" };
			var response = await httpClient.PostAsJsonAsync(guild.DiscordWebhookUrl, json);
			response.EnsureSuccessStatusCode();
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
	}

	var t1 = lootService.RefreshLoots(guildId);
	var t2 = lootService.RefreshRequests(guildId);
	await Task.WhenAll(t1, t2);

	// export raidloot to rotloot
	if (!string.IsNullOrEmpty(rotLootUrl))
	{
		var leftoverLootQuantityMap = await db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.RaidQuantity > 0)
			.ToDictionaryAsync(x => x.Id, x => x.RaidQuantity);

		var res = await httpClient.PostAsJsonAsync(rotLootUrl + "/ImportRaidLoot", leftoverLootQuantityMap);
		res.EnsureSuccessStatusCode();

		// reset all raid loot quantity to zero
		await db.Loots
			.Where(x => x.GuildId == guildId)
			.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, 0));
	}
});

// TODO: remove
app.MapPost("ImportRaidLoot", async (LootGodContext db, LootService lootService, IDictionary<int, byte> lootQuantityMap) =>
{
	await lootService.EnsureAdminStatus();
	var guildId = await lootService.GetGuildId();

	var loots = await db.Loots
		.Where(x => lootQuantityMap.Keys.Contains(x.Id))
		.ToListAsync();

	foreach (var loot in loots)
	{
		loot.RaidQuantity += lootQuantityMap[loot.Id];
	}

	await db.SaveChangesAsync();

	await lootService.RefreshLoots(guildId);
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
		.Where(x => nameToClassMap.Keys.Contains(x.Name))
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
		.ToDictionaryAsync(x => x.Id, x => (x.Name, x.RankId, x.Hidden));
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
		.Select(x => new
		{
			Name = x.Key.Name,
			Hidden = x.Key.Hidden,
			Rank = x.Key.RankId is null ? "unknown" : rankIdToNameMap[x.Key.RankId.Value],

			_30 = Math.Round(100.0 * x.Value.Count(y => y > thirty) / thirtyDayMaxCount, 0, MidpointRounding.AwayFromZero),
			_90 = Math.Round(100.0 * x.Value.Count(y => y > ninety) / ninetyDayMaxCount, 0, MidpointRounding.AwayFromZero),
			_180 = Math.Round(100.0 * x.Value.Count() / oneHundredEightDayMaxCount, 0, MidpointRounding.AwayFromZero),
		})
		.OrderBy(x => x.Name)
		.ToArray();
});

app.MapGet("GetGrantedLootOutput", async (LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var output = await lootService.GetGrantedLootOutput();
	var bytes = Encoding.UTF8.GetBytes(output);

	return Results.File(bytes, contentType: "text/plain", fileDownloadName: "RaidLootOutput-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
});

await app.RunAsync();

public record CreateLoot(byte Quantity, string Name);