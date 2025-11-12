using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

public class ImportService(ILogger<LootService> _logger, LootGodContext _db, LootService _lootService)
{
	private static readonly ActivitySource source = new(nameof(ImportService));

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

	private static async IAsyncEnumerable<GuildDumpPlayerOutput> ParseGuildDump(IFormFile file, [EnumeratorCancellation] CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(ParseGuildDump));
		await using var stream = file.OpenReadStream();
		using var sr = new StreamReader(stream);

		while (await sr.ReadLineAsync(token) is string line)
		{
			yield return new(line);
		}
	}

	public async Task ImportRaidDump(Stream stream, string fileName, int offset, CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(ImportRaidDump));

		var guildId = _lootService.GetGuildId();
		var timestamp = ParseTimestamp(fileName, offset);
		using var sr = new StreamReader(stream);
		var output = await sr.ReadToEndAsync(token);
		var nameToClassMap = output
			.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Split('\t'))
			.Where(x => x.Length > 4) // filter out "missing" rows that start with a number, but have nothing after
			.ToDictionary(x => x[1], x => x[3]);

		activity?.AddEvent(new("Raid dump parsed"));

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

		activity?.AddEvent(new("Players saved"));

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

		activity?.AddEvent(new("Raid dumps saved"));

		using var _ = _logger.BeginScope(new
		{
			StreamLength = output.Length,
			RaidPlayerCount = nameToClassMap.Count,
			InnerFileName = fileName,
			FileNameTimestamp = timestamp,
			PlayerCreatedCount = playerCreatedCount,
			RaidDumpCreatedCount = raidDumpCreatedCount,
		});
		_logger.RaidDumpImportCompleted();
	}

	public async Task BulkImportRaidDump(IFormFile file, int offset, CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(BulkImportRaidDump));
		await using var stream = file.OpenReadStream();
		using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
		using var _ = _logger.BeginScope(new { ZipEntryCount = zip.Entries.Count });

		foreach (var entry in zip.Entries.OrderBy(x => x.LastWriteTime))
		{
			await using var dump = entry.Open();
			await ImportRaidDump(dump, entry.FullName, offset, token);
		}
	}

	public async Task ImportRaidDump(IFormFile file, int offset, CancellationToken token)
	{
		await using var stream = file.OpenReadStream();
		await ImportRaidDump(stream, file.FileName, offset, token);
	}

	public async Task ImportGuildDump(IFormFile file, CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(ImportGuildDump));
		var guildId = _lootService.GetGuildId();
		var dumps = await ParseGuildDump(file, token).ToListAsync(token);

		activity?.AddEvent(new("Guild dump parsed"));

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

		activity?.AddEvent(new("Ranks created"));

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

			player.Guest = false;
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

		activity?.AddEvent(new("Preexisting players updated"));

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

		activity?.AddEvent(new("Players created"));

		using var _ = _logger.BeginScope(new
		{
			DumpCount = dumps.Count,
			RankCreatedCount = rankCreatedCount,
			PlayerUpdatedCount = playerUpdatedCount,
			PlayerCreatedCount = playerCreatedCount,
		});
		_logger.GuildDumpImportCompleted();
	}
}
