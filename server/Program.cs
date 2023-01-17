using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// using (var reader = File.OpenRead("C:\\Users\\bmarder\\Desktop\\itemlist.txt.gz"))
// using (var zip = new GZipStream(reader, CompressionMode.Decompress, true))
// using (var unzip = new StreamReader(zip))
// 	while (!unzip.EndOfStream)
// 		Console.WriteLine(await unzip.ReadLineAsync());
// Debug.Fail("");

var builder = WebApplication.CreateBuilder(args);
var source = Environment.GetEnvironmentVariable("DATABASE_URL");
var rotLootUrl = Environment.GetEnvironmentVariable("ROT_LOOT_URL");
var aspnetcore_urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var password = Environment.GetEnvironmentVariable("PASSWORD");
using var httpClient = new HttpClient();

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={source};"));
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

	foreach (var item in StaticData.NosLoot)
	{
		if (!db.Loots.Any(x => x.GuildId == 2 && x.Name == item))
		{
			db.Loots.Add(new(item, Expansion.NoS, 2));
		}
	}

	_ = await db.SaveChangesAsync();
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
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
	app.UseCors();
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<LootHub>("/lootHub");
app.UseOutputCache();
app.MapGet("/test", () => "Hello World!");

app.MapPost("NewLootNoS", async (LootGodContext db, string name, int guildId) =>
{
	var loot = new Loot(name, Expansion.NoS, guildId) { RaidQuantity = 1 };
	db.Loots.Add(loot);
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

	//var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
	//var requestCount = await db.LootRequests.CountAsync(x => x.IP == ip && x.CreatedDate > oneWeekAgo);
	//if (requestCount > 100) { throw new Exception("Limit Break - More than 100 requests per week from single IP"); }

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

// TODO: raid/rot loot locking, merge lock api
app.MapPost("EnableLootLock", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == guildId);
	guild!.RaidLootLocked = true;
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(guildId, true);
});

app.MapPost("DisableLootLock", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == guildId);
	guild!.RaidLootLocked = false;
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(guildId, false);
});

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

app.MapPost("GrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.Where(x => x.Player.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, true));

	await lootService.RefreshRequests(guildId);
});

app.MapPost("UngrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.Where(x => x.Player.GuildId == guildId)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, false));

	await lootService.RefreshRequests(guildId);
});

app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();
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

	// export raidloot to rotloot
	if (!string.IsNullOrEmpty(rotLootUrl))
	{
		var leftoverLootQuantityMap = await db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.RaidQuantity > 0)
			.ToDictionaryAsync(x => x.Id, x => x.RaidQuantity);

		var res = await httpClient.PostAsJsonAsync(rotLootUrl + "/ImportRaidLoot", leftoverLootQuantityMap);
		res.EnsureSuccessStatusCode();

		// reset all loot quantity to zero
		await db.Loots
			.Where(x => x.GuildId == guildId)
			.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, 0));
	}

	var t1 = lootService.RefreshLoots(guildId);
	var t2 = lootService.RefreshRequests(guildId);
	await Task.WhenAll(t1, t2);
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
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
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


app.MapGet("GetGrantedLootOutput", async (LootGodContext db, LootService lootService) =>
{
	await lootService.EnsureAdminStatus();

	var guildId = await lootService.GetGuildId();

	var items = (await db.LootRequests
		.Include(x => x.Loot)
		.Include(x => x.Player)
		.Where(x => x.Player.GuildId == guildId)
		.Where(x => x.Granted && !x.Archived)
		.OrderBy(x => x.LootId)
		.ThenBy(x => x.AltName ?? x.Player.Name)
		.ToListAsync())
		.GroupBy(x => (x.LootId, x.AltName ?? x.Player.Name))
		.Select(x =>
		{
			var request = x.First();
			return $"{request.Loot.Name} | {request.AltName ?? request.Player.Name} | x{x.Sum(y => y.Quantity)}";
		});

	var output = string.Join(Environment.NewLine, items);
	var bytes = Encoding.UTF8.GetBytes(output);

	return Results.File(bytes, contentType: "text/plain", fileDownloadName: "RaidLootOutput-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
});

await app.RunAsync();

public class CreateLoginAttempt
{
	public string MainName { get; set; } = null!;
	public string Password { get; set; } = null!;
}
public class CreateLoot
{
	public byte Quantity { get; set; }
	public string Name { get; set; } = null!;
}