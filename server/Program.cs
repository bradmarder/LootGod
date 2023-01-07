using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Net;

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

// temporary link to old loot database file
//builder.Services.AddDbContext<OldContext>(x => x.UseSqlite($"Data Source=/mnt/loot.sqlite;"));

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

	foreach (var item in StaticData.CoVLoots)
	{
		if (!db.Loots.Any(x => x.Name == item))
		{
			db.Loots.Add(new(item, Expansion.CoV));
		}
	}
	foreach (var item in StaticData.ToLLoots)
	{
		if (!db.Loots.Any(x => x.Name == item))
		{
			db.Loots.Add(new(item, Expansion.ToL));
		}
	}

	_ = await db.SaveChangesAsync();

	try
	{
		await db.Database.ExecuteSqlRawAsync(
"""
CREATE TABLE "Ranks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Ranks" PRIMARY KEY AUTOINCREMENT,
    "CreatedDate" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "Name" TEXT NOT NULL
);
""");
		await db.Database.ExecuteSqlRawAsync("ALTER TABLE Players ADD RankId INTEGER NULL;");
		await db.Database.ExecuteSqlRawAsync("ALTER TABLE Players ADD Hidden INTEGER NOT NULL default 0;");
	}
	catch { }
}

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

//app.MapPost("login", async (CreateLoginAttempt dto, HttpContext context, LootGodContext db) =>
//{
//	var ip = GetIPAddress(context) ?? "";

//	_ = db.LoginAttempts.Add(new(dto.MainName, ip));
//	_ = await db.SaveChangesAsync();

//	return dto.Password == password;
//});

// var swapComplete = false;
// app.MapGet("SwapDB", async (LootGodContext newDb, OldContext oldDb) =>
// {
// 	if (swapComplete) { return; }
	
// 	var req = await oldDb.LootRequests.ToListAsync();
// 	var login = await oldDb.LoginAttempts.ToListAsync();

// 	newDb.LootRequests.AddRange(req);
// 	newDb.LoginAttempts.AddRange(login);

// 	await newDb.SaveChangesAsync();
// 	swapComplete = true;
// });

app.MapGet("GetLootRequests", async (LootGodContext db) =>
{
	return (await db.LootRequests
		.Include(x => x.Player)
		.Where(x => !x.Archived)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.AltName ?? x.Player.Name)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetArchivedLootRequests", async (LootGodContext db, string? name, int? lootId) =>
{
	return (await db.LootRequests
		.Include(x => x.Player)
		.Where(x => x.Archived)
		.Where(x => name == null || EF.Functions.Like(x.AltName!, name) || EF.Functions.Like(x.Player.Name, name) )
		.Where(x => lootId == null || x.LootId == lootId)
		.OrderByDescending(x => x.Spell != null)
		.ThenBy(x => x.LootId)
		.ThenByDescending(x => x.AltName ?? x.Player.Name)
		.ToListAsync())
		.Select(x => new LootRequestDto(x));
});

app.MapGet("GetLoots", async (LootGodContext db) =>
{
	return (await db.Loots
		.Where(x => x.Expansion == Expansion.ToL)
		.OrderBy(x => x.Name)
		.ToListAsync())
		.Select(x => new LootDto(x));
});

// TODO: this should take lootId
app.MapPost("UpdateLootQuantity", async (CreateLoot dto, LootGodContext db, LootService lootService) =>
{
	await db.Loots
		.Where(x => x.Name == dto.Name)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, dto.Quantity));

	await lootService.RefreshLoots();
});

app.MapPost("ToggleHiddenPlayer", async (string playerName, LootGodContext db) =>
{
	await db.Players
		.Where(x => x.Name == playerName)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Hidden, y => !y.Hidden));
});

app.MapPost("CreateLootRequest", async (CreateLootRequest dto, LootGodContext db, LootService lootService) =>
{
	//var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
	//var requestCount = await db.LootRequests.CountAsync(x => x.IP == ip && x.CreatedDate > oneWeekAgo);
	//if (requestCount > 100) { throw new Exception("Limit Break - More than 100 requests per week from single IP"); }

	var playerId = await lootService.GetPlayerId();
	var ip = lootService.GetIPAddress();
	var item = new LootRequest(dto, ip, playerId);
	_ = db.LootRequests.Add(item);
	_ = await db.SaveChangesAsync();

	await lootService.RefreshRequests();
});

app.MapPost("DeleteLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteDeleteAsync();

	await lootService.RefreshRequests();
});

app.MapPost("IncrementLootQuantity", async (LootGodContext db, int id, LootService lootService) =>
{
	await db.Loots
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, y => y.RaidQuantity + 1));

	await lootService.RefreshLoots();
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

	await db.Loots
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, y => y.RaidQuantity == 0 ? 0 : y.RaidQuantity - 1));

	await lootService.RefreshLoots();
});

app.MapPost("EnableLootLock", async (LootGodContext db, LootService lootService) =>
{
	var ip = lootService.GetIPAddress();
	_ = db.LootLocks.Add(new(true, ip));
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(true);
});

app.MapPost("DisableLootLock", async (LootGodContext db, LootService lootService) =>
{
	var ip = lootService.GetIPAddress();
	_ = db.LootLocks.Add(new(false, ip));
	_ = await db.SaveChangesAsync();

	await lootService.RefreshLock(false);
});

app.MapGet("GetLootLock", async (LootGodContext db) =>
{
	var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

	return lootLock?.Lock ?? false;
});

app.MapPost("GrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, true));

	await lootService.RefreshRequests();
});

app.MapPost("UngrantLootRequest", async (LootGodContext db, HttpContext context, LootService lootService) =>
{
	var id = int.Parse(context.Request.Query["id"]!);
	await db.LootRequests
		.Where(x => x.Id == id)
		.ExecuteUpdateAsync(x => x.SetProperty(y => y.Granted, false));

	await lootService.RefreshRequests();
});

app.MapPost("FinishLootRequests", async (LootGodContext db, LootService lootService) =>
{
	var items = await db.LootRequests
		.Include(x => x.Loot)
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
			.Where(x => x.RaidQuantity > 0)
			.ToDictionaryAsync(x => x.Id, x => x.RaidQuantity);

		var res = await httpClient.PostAsJsonAsync(rotLootUrl + "/ImportRaidLoot", leftoverLootQuantityMap);
		res.EnsureSuccessStatusCode();

		// reset all loot quantity to zero
		await db.Loots.ExecuteUpdateAsync(x => x.SetProperty(y => y.RaidQuantity, 0));
	}

	var t1 = lootService.RefreshLoots();
	var t2 = lootService.RefreshRequests();
	await Task.WhenAll(t1, t2);
});

app.MapPost("ImportRaidLoot", async (LootGodContext db, LootService lootService, IDictionary<int, byte> lootQuantityMap) =>
{
	var loots = await db.Loots
		.Where(x => lootQuantityMap.Keys.Contains(x.Id))
		.ToListAsync();

	foreach (var loot in loots)
	{
		loot.RaidQuantity += lootQuantityMap[loot.Id];
	}

	await db.SaveChangesAsync();

	await lootService.RefreshLoots();
});

app.MapPost("ImportGuildDump", async (LootGodContext db, IFormFile file) =>
{
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
	var existingRankNames = await db.Ranks.Select(x => x.Name).ToListAsync();
	var ranks = dumps
		.Select(x => x.Rank)
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.Except(existingRankNames);
	foreach (var rank in ranks)
	{
		db.Ranks.Add(new(rank));
	}
	await db.SaveChangesAsync();

	// load all ranks
	var rankNameToIdMap = await db.Ranks.ToDictionaryAsync(x => x.Name, x => x.Id);

	// update existing players
	var players = await db.Players.ToArrayAsync(); // load per guild
	foreach (var player in players)
	{
		var dump = dumps.SingleOrDefault(x => x.Name == player.Name);
		if (dump is not null)
		{
			player.IsActive = true;
			player.RankId = rankNameToIdMap[dump.Rank];
			player.LastOnDate = dump.LastOnDate;
			player.Level = dump.Level;
			player.Alt = dump.Alt;
		}
		else
		{
			// if a player no longer appears in a guild dump output, we assert them inactive
			player.IsActive = false;
		}
	}
	await db.SaveChangesAsync();

	// create players who do not exist
	var existingNames = players
		.Select(x => x.Name)
		.ToHashSet();
	var dumpPlayers = dumps
		.Where(x => !existingNames.Contains(x.Name))
		.Select(x => new Player(x))
		.ToList();
	db.Players.AddRange(dumpPlayers);
	await db.SaveChangesAsync();
});

app.MapPost("ImportRaidDump", async (LootGodContext db, IFormFile file) =>
{
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
		.Select(x => x.Name)
		.ToArrayAsync();
	var players = nameToClassMap.Keys
		.Except(existingNames)
		.Select(x => new Player(x, nameToClassMap[x]))
		.ToList();
	db.Players.AddRange(players);
	await db.SaveChangesAsync();

	// save raid dumps for all players
	// example filename = RaidRoster_firiona-20220815-205645.txt
	var parts = file.FileName.Split('-');
	var time = parts[1] + parts[2].Split('.')[0];
	var timestamp = DateTime.ParseExact(time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
	var raidDumps = (await db.Players
		.Where(x => nameToClassMap.Keys.Contains(x.Name))
		.Select(x => x.Id)
		.ToArrayAsync())
		.Select(x => new RaidDump(timestamp, x))
		.ToArray();
	db.RaidDumps.AddRange(raidDumps);
	await db.SaveChangesAsync();
});

app.MapPost("BulkImportRaidDump", async (LootGodContext db, IFormFile file) =>
{
	await using var stream = file.OpenReadStream();
	using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

	foreach (var entry in zip.Entries)
	{
		await using var dump = entry.Open();
		using var sr = new StreamReader(dump);
		var data = await sr.ReadToEndAsync();
		using var content = new StringContent(data);
		using var form = new MultipartFormDataContent
		{
			{ content, "file", entry.FullName }
		};
		var port = aspnetcore_urls!.Split(':').Last();
		var res = await httpClient.PostAsync($"http://{IPAddress.Loopback}:{port}/ImportRaidDump", form);
		res.EnsureSuccessStatusCode();
	}
});

app.MapGet("GetPlayerAttendance", async (LootGodContext db) =>
{
	var oneHundredEightyDaysAgo = DateTime.UtcNow.AddDays(-180);
	var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
	var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
	var ninety = DateOnly.FromDateTime(ninetyDaysAgo);
	var thirty = DateOnly.FromDateTime(thirtyDaysAgo);

	var playerMap = await db.Players.ToDictionaryAsync(x => x.Id, x => (x.Name, x.RankId, x.Hidden));
	var rankIdToNameMap = await db.Ranks.ToDictionaryAsync(x => x.Id, x => x.Name);
	var dumps = await db.RaidDumps
		.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
		.ToListAsync();
	var uniqueDates = dumps
		.Select(x => DateOnly.FromDateTime(x.Timestamp))
		.ToHashSet();
	var oneHundredEightDayMaxCount = uniqueDates.Count;
	var ninetyDayMaxCount = uniqueDates.Count(x => x > ninety);
	var thirtyDayMaxCount = uniqueDates.Count(x => x > thirty);

	return dumps
		.GroupBy(x => x.PlayerId)
		.ToDictionary(x => playerMap[x.Key], x => x.Select(y => DateOnly.FromDateTime(y.Timestamp)).ToHashSet())
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

app.MapGet("GetTestPlayerAttendance", async (LootGodContext db) =>
{
	var oneHundredEightyDaysAgo = DateTime.UtcNow.AddDays(-180);
	var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
	var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

	var playerMap = await db.Players.ToDictionaryAsync(x => x.Id, x => (x.Name, x.RankId, x.Hidden));
	var rankIdToNameMap = await db.Ranks.ToDictionaryAsync(x => x.Id, x => x.Name);
	var dumps = await db.RaidDumps
		.Where(x => x.Timestamp > oneHundredEightyDaysAgo)
		.ToListAsync();
	var uniqueTimestamps = dumps
		.Select(x => x.Timestamp)
		.ToHashSet();
	var oneHundredEightDayMaxCount = uniqueTimestamps.Count;
	var ninetyDayMaxCount = uniqueTimestamps.Count(x => x > ninetyDaysAgo);
	var thirtyDayMaxCount = uniqueTimestamps.Count(x => x > thirtyDaysAgo);

	return dumps
		.GroupBy(x => x.PlayerId)
		.ToDictionary(x => playerMap[x.Key], x => x)
		.Select(x => new
		{
			Name = x.Key.Name,
			Hidden = x.Key.Hidden,
			Rank = x.Key.RankId is null ? "unknown" : rankIdToNameMap[x.Key.RankId.Value],

			_30 = Math.Round(100.0 * x.Value.Count(y => y.Timestamp > thirtyDaysAgo) / thirtyDayMaxCount, 0, MidpointRounding.AwayFromZero),
			_90 = Math.Round(100.0 * x.Value.Count(y => y.Timestamp > ninetyDaysAgo) / ninetyDayMaxCount, 0, MidpointRounding.AwayFromZero),
			_180 = Math.Round(100.0 * x.Value.Count(y => y.Timestamp > oneHundredEightyDaysAgo) / oneHundredEightDayMaxCount, 0, MidpointRounding.AwayFromZero),
		})
		.OrderBy(x => x.Name)
		.ToArray();
});

app.MapGet("GetGrantedLootOutput", async (LootGodContext db) =>
{
	var items = (await db.LootRequests
		.Include(x => x.Loot)
		.Include(x => x.Player)
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

	return string.Join(Environment.NewLine, items);
});

await app.RunAsync();

public class LootHub : Hub { }
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