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
	private record Counter
	{
		public int Total { get; private set; }
		public void Increment() => Total++;
	}

	public const int ManualItemMinId = 1_000_000_000;
	public const int ManualItemMaxId = 1_001_000_000;
	private const string ItemDataUrl = "https://items.sodeq.org/downloads/items.txt.gz";
	private const string SpellDataUrl = "https://lucy.allakhazam.com/static/spelldata/spelldata_Live_2025-12-03_05:26:16.txt.gz";

	private static readonly ActivitySource source = new(nameof(SyncService));

	public async Task DataSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(DataSync));

		/// disable FK constraints during sync because <see cref="OnConflictInterceptor"/> will *DELETE* and re-insert rows on conflict, which combined
		/// with "ON DELETE CASCADE" will delete all related rows in loots/lootRequests
		/// Transactions don't help here
		_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
		await SpellSync(token);
		await ItemSync(token);
		_db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");

		ManualItemSync();
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

	private async IAsyncEnumerable<int?> GetSpellEffectIds([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var line in FetchLines(ItemDataUrl, cancellationToken))
		{
			var item = new ItemParseOutput(line);
			if (item.IsRaid)
			{
				yield return item.ProcEffect;
				yield return item.ClickEffect;
				yield return item.WornEffect;
				yield return item.FocusEffect;
				yield return item.EMFocusEffect;
			}
		}
	}

	private async Task SpellSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(SpellSync));

		var watch = Stopwatch.StartNew();
		var counter = new Counter();
		var spellEffectIds = await GetSpellEffectIds(token)
			.Where(x => x > 0)
			.ToHashSetAsync(cancellationToken: token);
		var spells = await GetSpells(counter, spellEffectIds, token).ToArrayAsync(token);

		/// ON CONFLICT REPLACE <see cref="OnConflictInterceptor"/>
		_db.AddRange(spells);
		_db.SaveChanges();

		_logger.SpellSyncSuccess(spells.Length, counter.Total, watch.ElapsedMilliseconds);
	}

	private async IAsyncEnumerable<Spell> GetSpells(Counter counter, ISet<int?> spellEffectIds, [EnumeratorCancellation] CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(GetSpells));

		var now = _time.GetUtcNow().ToUnixTimeSeconds();

		await foreach (var line in FetchLines(SpellDataUrl, token))
		{
			counter.Increment();
			var output = new SpellParseOutput(line);

			if (spellEffectIds.Contains(output.Id) || output.IsRaid)
			{
				yield return new(output, now);
			}
		}
	}

	private async IAsyncEnumerable<Item> GetItems(Counter counter, [EnumeratorCancellation] CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(GetItems));

		var now = _time.GetUtcNow().ToUnixTimeSeconds();

		await foreach (var line in FetchLines(ItemDataUrl, token))
		{
			counter.Increment();
			var item = new ItemParseOutput(line);
			if (item.IsRaid)
			{
				yield return ItemMapper.ItemOutputMap(item, now);
			}
		}
	}

	private async Task ItemSync(CancellationToken token)
	{
		using var activity = source.StartActivity(nameof(ItemSync));
		
		var watch = Stopwatch.StartNew();
		var counter = new Counter();
		var items = await GetItems(counter, token).ToArrayAsync(token);

		/// ON CONFLICT REPLACE <see cref="OnConflictInterceptor"/>
		_db.Items.AddRange(items);
		_db.SaveChanges();

		_logger.ItemSyncSuccess(items.Length, counter.Total, watch.ElapsedMilliseconds);
	}

	private void ManualItemSync()
	{
		var manualItems = _db.Items
			.Where(x => x.Sync == 0)
			.Where(x => x.Id > ManualItemMinId && x.Id < ManualItemMaxId)
			.ToArray();
		var manualNames = manualItems
			.Select(x => x.Name.ToLower())
			.ToArray();
		var oneYearAgo = _time.GetUtcNow().AddYears(-1).ToUnixTimeSeconds();
		var syncItems = _db.Items
			.Where(x => x.Sync > oneYearAgo)
			.Where(x => manualNames.Contains(x.Name.ToLower()))
			.ToArray();
		var manualIdToSyncIdMap = Enumerable
			.Join(manualItems, syncItems, x => x.Name, x => x.Name, (x, y) => (x.Id, y.Id))
			.ToDictionary(x => x.Item1, x => x.Item2);
		var manualItemIds = manualIdToSyncIdMap
			.Select(x => x.Key)
			.ToArray();
		var requests = _db.LootRequests
			.Where(x => manualItemIds.Contains(x.ItemId))
			.ToArray();
		var loots = _db.Loots
			.Where(x => manualItemIds.Contains(x.ItemId))
			.ToArray();

		// update loots + loot requests FK to point to latest synced item
		foreach (var loot in loots)
		{
			loot.ItemId = manualIdToSyncIdMap[loot.ItemId];
		}
		foreach (var req in requests)
		{
			req.ItemId = manualIdToSyncIdMap[req.ItemId];
		}
		_db.SaveChanges();

		// delete the manually added items
		_db.Items
			.Where(x => manualItemIds.Contains(x.Id))
			.ExecuteDelete();

		_logger.ManualItemSync(manualItemIds.Length, loots.Length, requests.Length);
	}
}