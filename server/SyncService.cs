using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;

public class SyncService(
	TimeProvider _time,
	ILogger<SyncService> _logger,
	LootGodContext _db,
	HttpClient _httpClient)
{
	public async Task DataSync()
	{
		_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

		await SpellSync();
		await ItemSync();

		_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
	}

	private async IAsyncEnumerable<string> FetchLines(string requestUri, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
		using var reader = new StreamReader(gzip);

		// skip header
		await reader.ReadLineAsync(cancellationToken);

		while (await reader.ReadLineAsync(cancellationToken) is string line)
		{
			yield return line;
		}
	}
	private async Task SpellSync()
	{
		var now = _time.GetUtcNow().ToUnixTimeSeconds();
		var watch = Stopwatch.StartNew();
		var spells = new List<Spell>();
		var deletedCount = 0;

		await foreach (var line in FetchLines("https://lucy.allakhazam.com/static/spelldata/spelldata_Live_2025-08-27_01:59:10.txt.gz", CancellationToken.None))
		{
			var output = new SpellParseOutput(line);
			spells.Add(new(output, now));
		}

		using (var transaction = _db.Database.BeginTransaction())
		{
			try
			{
				deletedCount = _db.Spells.ExecuteDelete();
				_db.Spells.AddRange(spells);
				_db.SaveChanges();
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		var state = new
		{
			ElapsedMs = watch.ElapsedMilliseconds,
			DeletedCount = deletedCount,
			SpellCount = spells.Count,
		};
		using var _ = _logger.BeginScope(state);
		_logger.LogInformation("Successfully completed spell sync");
	}

	private async Task ItemSync()
	{
		var now = _time.GetUtcNow().ToUnixTimeSeconds();
		var watch = Stopwatch.StartNew();
		var raidItems = new List<Item>();
		var totalItemCount = 0;
		var deletedCount = 0;

		await foreach (var line in FetchLines("https://items.sodeq.org/downloads/items.txt.gz", CancellationToken.None))
		{
			totalItemCount++;
			var item = new ItemParseOutput(line);
			if (item is { IsRaid: true, Expansion: not Expansion.Unknown })
			{
				raidItems.Add(ItemMapper.ItemOutputMap(item, now));
			}
		}

		using (var transaction = _db.Database.BeginTransaction())
		{
			try
			{
				deletedCount = _db.Items.ExecuteDelete();
				_db.Items.AddRange(raidItems);
				_db.SaveChanges();
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		var state = new
		{
			ElapsedMs = watch.ElapsedMilliseconds,
			DeletedCount = deletedCount,
			RaidItemCount = raidItems.Count,
			TotalItemCount = totalItemCount,
		};
		using var _ = _logger.BeginScope(state);
		_logger.LogInformation("Successfully completed item sync");
	}
}
