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
	private const string ItemDataUrl = "https://items.sodeq.org/downloads/items.txt.gz";
	private const string SpellDataUrl = "https://lucy.allakhazam.com/static/spelldata/spelldata_Live_2025-12-03_05:26:16.txt.gz";

	private static readonly ActivitySource source = new(nameof(SyncService));

	public async Task DataSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(DataSync));

		_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

		using (var transaction = _db.Database.BeginTransaction())
		{
			try
			{
				await ItemSync(token);
				await SpellSync(token);
				_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				// _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
				throw;
			}
		}
	}

	private async IAsyncEnumerable<string> FetchLines(string requestUri, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		using var activity = source.StartActivity(nameof(FetchLines));
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

	private async Task SpellSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(SpellSync));
		var now = _time.GetUtcNow().ToUnixTimeSeconds();
		var watch = Stopwatch.StartNew();
		var totalSpellCount = 0;

		var procIds = _db.Items.Select(x => x.ProcEffect).Where(x => x != null).ToHashSet();
		var focusIds = _db.Items.Select(x => x.FocusEffect).Where(x => x != null).ToHashSet();
		var clickIds = _db.Items.Select(x => x.ClickEffect).Where(x => x != null).ToHashSet();
		var wornIds = _db.Items.Select(x => x.WornEffect).Where(x => x != null).ToHashSet();
		var emFocusIds = _db.Items.Select(x => x.EMFocusEffect).Where(x => x != null).ToHashSet();
		HashSet<int?> spellIds = [.. procIds, .. focusIds, .. clickIds, .. wornIds, .. emFocusIds];

		await foreach (var line in FetchLines(SpellDataUrl, token))
		{
			totalSpellCount++;
			var output = new SpellParseOutput(line);
			if (spellIds.Contains(output.Id))
			{
				_db.Spells.Add(new(output, now));
			}
		}

		/// ON CONFLICT REPLACE <see cref="OnConflictInterceptor"/>
		var count = _db.SaveChanges();

		var state = new
		{
			ElapsedMs = watch.ElapsedMilliseconds,
			SpellCount = count,
			TotalSpellCount = totalSpellCount,
		};
		using var _ = _logger.BeginScope(state);
		_logger.SpellSyncSuccess();
	}

	private async Task ItemSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(ItemSync));
		var now = _time.GetUtcNow().ToUnixTimeSeconds();
		var watch = Stopwatch.StartNew();
		var totalItemCount = 0;

		await foreach (var line in FetchLines(ItemDataUrl, token))
		{
			totalItemCount++;
			var item = new ItemParseOutput(line);
			if (item is { IsRaid: true, Expansion: not Expansion.Unknown })
			{
				_db.Items.Add(ItemMapper.ItemOutputMap(item, now));
			}
		}

		/// ON CONFLICT REPLACE <see cref="OnConflictInterceptor"/>
		var count = _db.SaveChanges();

		var state = new
		{
			ElapsedMs = watch.ElapsedMilliseconds,
			RaidItemCount = count,
			TotalItemCount = totalItemCount,
		};
		using var _ = _logger.BeginScope(state);
		_logger.ItemSyncSuccess();
	}
}