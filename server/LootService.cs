using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

public class LootService(
	ILogger<LootService> _logger,
	IHttpContextAccessor _httpContextAccessor,
	LootGodContext _db,
	HttpClient _httpClient,
	Channel<Payload> _payloadChannel,
	ConcurrentDictionary<string, DataSink> _dataSinks)
{
	private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private static readonly Expansion[] CurrentExpansions = [Expansion.NoS, Expansion.LS];
	private static readonly ActivitySource source = new(nameof(LootService));

	private HttpRequest Request => _httpContextAccessor.HttpContext!.Request;

	public Guid? GetPlayerKey() =>
		Request.Headers.TryGetValue("Player-Key", out var headerKey) ? Guid.Parse(headerKey.ToString())
		: Request.Query.TryGetValue("playerKey", out var queryKey) ? Guid.Parse(queryKey.ToString())
		: null;

	public string? GetIPAddress() =>
		Request.Headers.TryGetValue("Fly-Client-IP", out var val)
			? val
			: _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

	public int GetPlayerId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Single(x => x.Key == key && x.Active != false)
			.Id;
	}

	public int GetGuildId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Single(x => x.Key == key && x.Active != false)
			.GuildId;
	}

	public bool GetAdminStatus()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Single(x => x.Key == key && x.Active == true)
			.Admin;
	}

	public bool IsGuildLeader()
	{
		var key = GetPlayerKey();

		return _db.Players.Any(x => x.Key == key && x.Rank!.Name == Rank.Leader);
	}

	public bool GetRaidLootLock()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Select(x => x.Guild.LootLocked)
			.Single();
	}

	public void EnsureAdminStatus()
	{
		if (!GetAdminStatus())
		{
			throw new UnauthorizedAccessException(GetPlayerKey().ToString());
		}
	}

	public void EnsureGuildLeader()
	{
		if (!IsGuildLeader())
		{
			throw new UnauthorizedAccessException(GetPlayerKey().ToString());
		}
	}

	public void EnsureRaidLootUnlocked()
	{
		if (GetRaidLootLock())
		{
			throw new Exception(nameof(EnsureRaidLootUnlocked));
		}
	}

	public string GetGrantedLootOutput(bool raidNight)
	{
		var guildId = GetGuildId();

		var grantedLoot = _db.LootRequests
			.Where(x => x.Player.GuildId == guildId)
			.Where(x => x.Granted && !x.Archived)
			.Where(x => x.RaidNight == raidNight)
			.GroupBy(x => new
			{
				ItemName = x.Item.Name,
				PlayerName = x.AltName ?? x.Player.Name,
			})
			.OrderBy(x => x.Key.ItemName)
			.ThenBy(x => x.Key.PlayerName)
			.Select(x => new LootOutput(x.Key.ItemName, x.Key.PlayerName, x.Sum(y => y.Quantity)))
			.ToArray();

		var rotLoot = _db.Loots
			.Where(_ => raidNight) // only show rot loot for raid night
			.Where(x => x.GuildId == guildId)
			.Where(x => x.RaidQuantity > 0)
			.Select(x => new
			{
				x.Item.Name,
				Quantity = x.RaidQuantity - x.Item.LootRequests.Count(x => x.Player.GuildId == guildId && x.Granted && !x.Archived && x.RaidNight == raidNight),
			})
			.Where(x => x.Quantity > 0)
			.Select(x => new LootOutput(x.Name, "ROT", x.Quantity))
			.ToArray();

		LootOutput[] lootAndRot = [.. grantedLoot, .. rotLoot];
		if (!lootAndRot.Any()) { return ""; }
		var maxLootLength = lootAndRot.Max(x => x.Loot.Length);
		var maxNameLength = lootAndRot.Max(x => x.Name.Length);
		var output = lootAndRot.Select(x => $"{x.Loot.PadRight(maxLootLength)} | {x.Name.PadRight(maxNameLength)} | x{x.Quantity}");

		return string.Join(Environment.NewLine, output);
	}

	public bool AddDataSink(string connectionId, HttpResponse response, CancellationToken token)
	{
		LogNewDataSink();

		var guildId = GetGuildId();
		var sink = new DataSink
		{
			GuildId = guildId,
			Response = response,
			Token = token,
		};
		return _dataSinks.TryAdd(connectionId, sink);
	}

	public bool RemoveDataSink(string connectionId) => _dataSinks.Remove(connectionId, out _);

	public ItemDto[] LoadItems()
	{
		return _db.Items
			.Where(x => EF.Constant(CurrentExpansions).Contains(x.Expansion))
			.OrderBy(x => x.Name)
			.Select(x => new ItemDto
			{
				Id = x.Id,
				Name = x.Name,
			})
			.ToArray();
	}

	public LootDto[] LoadLoots(int guildId)
	{
		return _db.Loots
			.Where(x => x.GuildId == EF.Constant(guildId))
			.Where(x => EF.Constant(CurrentExpansions).Contains(x.Item.Expansion))
			.Select(x => new LootDto
			{
				ItemId = x.ItemId,
				Name = x.Item.Name,
				RaidQuantity = x.RaidQuantity,
				RotQuantity = x.RotQuantity,
			})
			.ToArray()
			.OrderBy(x => x.Name)
			.ToArray();
	}

	public LootRequestDto[] LoadLootRequests(int guildId)
	{
		return _db.LootRequests
			.Where(x => x.Player.GuildId == EF.Constant(guildId))
			.Where(x => !x.Archived)
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
	}

	public async Task RefreshLock(int guildId, bool locked)
	{
		var json = locked ? "[true]" : "[false]";
		var payload = new Payload(guildId, "lock", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshMessageOfTheDay(int guildId, string motd)
	{
		var payload = new Payload(guildId, "motd", motd);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshItems()
	{
		var items = LoadItems();
		var json = JsonSerializer.Serialize(items, _jsonOptions);
		var payload = new Payload(null, "items", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = LoadLoots(guildId);
		var json = JsonSerializer.Serialize(loots, _jsonOptions);
		var payload = new Payload(guildId, "loots", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = LoadLootRequests(guildId);
		var json = JsonSerializer.Serialize(requests, _jsonOptions);
		var payload = new Payload(guildId, "requests", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task DiscordWebhook(string output, string discordWebhookUrl)
	{
		const string syntax = "coq";

		// A single bucket must be under the 2k max for discord (excludes backticks/newlines/emojis?)
		// Assume 1_700 max characters per bucket to safely account for splitting lines evenly
		var bucketCount = Math.Round(output.Length / 1_700d, MidpointRounding.ToPositiveInfinity);
		var lines = output.Split(Environment.NewLine);
		var maxLinesPerBucket = (int)Math.Round(lines.Length / bucketCount, MidpointRounding.ToPositiveInfinity);
		var buckets = lines.Chunk(maxLinesPerBucket);

		foreach (var bucket in buckets)
		{
			var data = string.Join(Environment.NewLine, bucket);
			var json = new { content = $"```{syntax}{Environment.NewLine}{data}{Environment.NewLine}```" };
			HttpResponseMessage? response = null;
			try
			{
				response = await _httpClient.PostAsJsonAsync(discordWebhookUrl, json);
				response.EnsureSuccessStatusCode();
			}
			catch (Exception ex)
			{
				var content = await TryReadContentAsync(response);
				_logger.LogError(ex, content);
			}
			finally
			{
				response?.Dispose();
			}
		}
	}

	/// <summary>
	/// example filename = RaidRoster_firiona-20220815-205645.txt
	/// </summary>
	private static long ParseTimestamp(string fileName, int offset)
	{
		var parts = fileName.Split('-');
		var time = parts[1] + parts[2].Split('.')[0];

		// since the filename of the raid dump doesn't include the timezone, we assume it matches the user's browser UTC offset
		return DateTimeOffset
			.ParseExact(time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
			.AddMinutes(offset)
			.ToUnixTimeSeconds();
	}

	public async Task ImportRaidDump(Stream stream, string fileName, int offset)
	{
		using var activity = source.StartActivity(nameof(ImportRaidDump));

		var guildId = GetGuildId();
		var timestamp = ParseTimestamp(fileName, offset);
		using var sr = new StreamReader(stream);
		var output = await sr.ReadToEndAsync();
		var nameToClassMap = output
			.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Split('\t'))
			.Where(x => x.Length > 4) // filter out "missing" rows that start with a number, but have nothing after
			.ToDictionary(x => x[1], x => x[3]);

		activity?.AddEvent(new ActivityEvent("File Read"));
		activity?.SetTag("StreamLength", stream.Length);
		activity?.SetTag("RaidPlayerCount", nameToClassMap.Count);
		activity?.SetTag("FileName", fileName);
		activity?.SetTag("FileNameTimestamp", timestamp);

		// create new players by comparing names with preexisting ones
		var existingNames = _db.Players
			.Where(x => x.GuildId == guildId)
			.Select(x => x.Name)
			.ToArray();
		var players = nameToClassMap.Keys
			.Except(existingNames)
			.Select(x => new Player(x, nameToClassMap[x], guildId))
			.ToList();
		_db.Players.AddRange(players);
		var playerCreatedCount = _db.SaveChanges();

		activity?.AddEvent(new ActivityEvent("Players Saved"));
		activity?.SetTag("PlayerCreatedCount", playerCreatedCount);

		// save raid dumps for all players
		var values = _db.Players
			.Where(x => x.GuildId == guildId)
			.Where(x => nameToClassMap.Keys.Contains(x.Name)) // ContainsKey cannot be translated by EFCore
			.Select(x => $"({x.Id}, {timestamp})")
			.ToArray();
		var sql = new StringBuilder()
			.AppendLine($"INSERT INTO '{nameof(RaidDump)}s' ('{nameof(RaidDump.PlayerId)}', '{nameof(RaidDump.Timestamp)}') VALUES")
			.AppendLine(string.Join(',', values))
			.AppendLine("ON CONFLICT DO NOTHING") // UPSERT - Ignore unique constraint on the primary composite key for RaidDump (Timestamp/Player)
			.ToString();
		var raidDumpCreatedCount = _db.Database.ExecuteSqlRaw(sql);

		activity?.AddEvent(new ActivityEvent("Raid Dumps Saved"));
		activity?.SetTag("RaidDumpCreatedCount", raidDumpCreatedCount);
	}

	public async Task BulkImportRaidDump(IFormFile file, int offset)
	{
		using var activity = source.StartActivity(nameof(BulkImportRaidDump));
		await using var stream = file.OpenReadStream();
		using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

		activity?.SetTag("EntryCount", zip.Entries.Count);

		foreach (var entry in zip.Entries.OrderBy(x => x.LastWriteTime))
		{
			await using var dump = entry.Open();
			await ImportRaidDump(dump, entry.FullName, offset);
		}
	}

	public async Task ImportRaidDump(IFormFile file, int offset)
	{
		await using var stream = file.OpenReadStream();
		await ImportRaidDump(stream, file.FileName, offset);
	}

	public async Task ImportGuildDump(IFormFile file)
	{
		using var activity = source.StartActivity(nameof(ImportGuildDump));
		var guildId = GetGuildId();

		var dumps = await ParseGuildDump(file);
		activity?.AddEvent(new ActivityEvent("Guild Dump Parsed"));
		activity?.SetTag("DumpCount", dumps.Length);

		// ensure not partial guild dump by checking a leader exists
		if (!dumps.Any(x => StringComparer.OrdinalIgnoreCase.Equals(Rank.Leader, x.Rank)))
		{
			throw new ImportException("Missing Leader Rank?! Ensure you select 'All' from the '# Per Page' dropdown from the bottom of the Guild Management window before creating a guild dump.");
		}

		// ensure guild leader does not change (must use TransferGuildLeadership endpoint instead)
		var existingLeader = _db.Players.Single(x => x.GuildId == guildId && x.Rank!.Name == Rank.Leader);
		if (!dumps.Any(x =>
			StringComparer.OrdinalIgnoreCase.Equals(x.Name, existingLeader.Name)
			&& StringComparer.OrdinalIgnoreCase.Equals(x.Rank, Rank.Leader)))
		{
			throw new ImportException("Cannot transfer guild leadership during a dump.");
		}

		// create the new ranks
		var existingRankNames = _db.Ranks
			.Where(x => x.GuildId == guildId)
			.Select(x => x.Name)
			.ToList();
		var ranks = dumps
			.Select(x => x.Rank)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Except(existingRankNames)
			.Select(x => new Rank(x, guildId))
			.ToArray();
		_db.Ranks.AddRange(ranks);
		var rankCreatedCount = _db.SaveChanges();

		activity?.AddEvent(new ActivityEvent("Ranks Created"));
		activity?.SetTag("RankCreatedCount", rankCreatedCount);

		// load all ranks
		var rankNameToIdMap = _db.Ranks
			.Where(x => x.GuildId == guildId)
			.ToDictionary(x => x.Name, x => x.Id);

		// update existing players
		var players = _db.Players
			.Where(x => x.GuildId == guildId)
			.ToArray();
		foreach (var player in players)
		{
			var dump = dumps.SingleOrDefault(x => x.Name == player.Name);

			// if a player no longer appears in a guild dump output, we assert them inactive
			if (dump is null)
			{
				player.Active = false;
				player.Admin = false;
				continue;
			}

			player.Active = true;
			player.RankId = rankNameToIdMap[dump.Rank];
			player.LastOnDate = dump.LastOnDate;
			player.Level = dump.Level;
			player.Alt = dump.Alt;
			player.Notes = dump.Notes;
			player.Zone = dump.Zone;

			// TODO: shouldn't be necessary, bug with Kyoto class defaulting to Bard
			player.Class = Player._classNameToEnumMap[dump.Class];

			// if a player switches their main to a previously linked alt, reset the MainId to null
			if (!dump.Alt) { player.MainId = null; }
		}
		var playerUpdatedCount = _db.SaveChanges();

		activity?.AddEvent(new ActivityEvent("Preexisting Players Updated"));
		activity?.SetTag("PlayerUpdatedCount", playerUpdatedCount);

		// create players who do not exist
		var existingNames = players
			.Select(x => x.Name)
			.ToHashSet();
		var dumpPlayers = dumps
			.Where(x => !existingNames.Contains(x.Name))
			.Select(x => new Player(x, guildId))
			.ToList();
		_db.Players.AddRange(dumpPlayers);
		var playerCreatedCount = _db.SaveChanges();

		activity?.AddEvent(new ActivityEvent("Players Created"));
		activity?.SetTag("PlayerCreatedCount", playerCreatedCount);
	}

	// parse the guild dump player output
	static async Task<GuildDumpPlayerOutput[]> ParseGuildDump(IFormFile file)
	{
		await using var stream = file.OpenReadStream();
		using var sr = new StreamReader(stream);
		var output = await sr.ReadToEndAsync();

		return output
			.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Split('\t'))
			.Select(x => new GuildDumpPlayerOutput(x))
			.ToArray();
	}

	private async Task<string> TryReadContentAsync(HttpResponseMessage? response)
	{
		try
		{
			return response is null ? "" : await response.Content.ReadAsStringAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, nameof(TryReadContentAsync));
			return "";
		}
	}

	private void LogNewDataSink()
	{
		var key = GetPlayerKey();
		var player = _db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.Single(x => x.Key == key);

		using var _ = _logger.BeginScope(new
		{
			IP = GetIPAddress(),
			Name = player.Name,
			GuildName = player.Guild.Name,
		});
		_logger.LogInformation(nameof(AddDataSink));
	}
}
