using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace LootGod;

public class LootService(ILogger<LootService> _logger, LootGodContext _db, IHttpContextAccessor _httpContextAccessor)
{
	private record DataSink(int GuildId, HttpResponse Response)
	{
		public int EventId { get; set; } = 1;
	}

	/// <summary>
	/// null GuildId implies the payload is sent to every client (items)
	/// </summary>
	private record Payload(int? GuildId, string Event, string JsonData);

	public record LootOutput(string Loot, string Name, int Quantity);

	private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private static readonly Channel<Payload> PayloadChannel = Channel.CreateUnbounded<Payload>(new() { SingleReader = true, SingleWriter = false });
	private static readonly ConcurrentDictionary<string, DataSink> DataSinks = new();

	public Guid? GetPlayerKey() =>
		_httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Player-Key", out var headerKey) ? Guid.Parse(headerKey.ToString())
		: _httpContextAccessor.HttpContext!.Request.Query.TryGetValue("playerKey", out var queryKey) ? Guid.Parse(queryKey.ToString())
		: null;

	public string? GetIPAddress() =>
		_httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Fly-Client-IP", out var val)
			? val
			: _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();

	public int GetPlayerId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active != false)
			.Select(x => x.Id)
			.Single();
	}

	public int GetGuildId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active != false)
			.Select(x => x.GuildId)
			.Single();
	}

	public bool GetAdminStatus()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active == true)
			.Select(x => x.Admin)
			.Single();
	}

	public bool IsGuildLeader()
	{
		var key = GetPlayerKey();

		return _db.Players.Any(x => x.Key == key && x.Rank!.Name == "Leader");
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
			throw new Exception(GetPlayerKey().ToString());
		}
	}

	public string GetGrantedLootOutput()
	{
		var guildId = GetGuildId();

		var grantedLoot = _db.LootRequests
			.AsNoTracking()
			.Include(x => x.Item)
			.Include(x => x.Player)
			.Where(x => x.Player.GuildId == guildId)
			.Where(x => x.Granted && !x.Archived)
			.OrderBy(x => x.ItemId)
			.ThenBy(x => x.AltName ?? x.Player.Name)
			.ToList()
			.GroupBy(x => (x.ItemId, x.AltName ?? x.Player.Name))
			.Select(x =>
			{
				var request = x.First();
				return new LootOutput(request.Item.Name, request.AltName ?? request.Player.Name, x.Sum(y => y.Quantity));
			})
			.ToArray();

		var rotLoot = _db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.RaidQuantity > 0)
			.Select(x => new
			{
				x.Item.Name,
				Quantity = x.RaidQuantity - x.Item.LootRequests.Count(x => x.Player.GuildId == guildId && x.Granted && !x.Archived),
			})
			.Where(x => x.Quantity > 0)
			.Select(x => new LootOutput(x.Name, "ROT", x.Quantity))
			.ToArray();

		var lootAndRot = grantedLoot.Concat(rotLoot).ToArray();
		if (lootAndRot.Length == 0) { return ""; }
		var maxLoot = lootAndRot.Max(x => x.Loot.Length);
		var maxName = lootAndRot.Max(x => x.Name.Length);
		var format = $"{{0,-{maxLoot + 1}}} | {{1,-{maxName + 2}}} | x{{2}}";
		var output = lootAndRot.Select(x => string.Format(format, x.Loot, x.Name, x.Quantity));

		return string.Join(Environment.NewLine, output);
	}

	public void AddDataSink(string connectionId, HttpResponse response)
	{
		LogNewDataSink();

		var sink = new DataSink(GetGuildId(), response);
		DataSinks.TryAdd(connectionId, sink);
	}

	public bool RemoveDataSink(string connectionId) => DataSinks.Remove(connectionId, out _);

	public async Task DeliverPayloads()
	{
		await foreach (var payload in PayloadChannel.Reader.ReadAllAsync())
		{
			var watch = Stopwatch.StartNew();

			foreach (var sink in DataSinks)
			{
				if (payload.GuildId is null || sink.Value.GuildId == payload.GuildId)
				{
					var text = new StringBuilder()
						.Append($"event: {payload.Event}\n")
						.Append($"data: {payload.JsonData}\n")
						.Append($"id: {sink.Value.EventId++}\n")
						.Append("\n\n")
						.ToString();
					var res = sink.Value.Response;
					try
					{
						await res.WriteAsync(text);
						await res.Body.FlushAsync();
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Orphan connection removed - {ConnectionId}", sink.Key);
						RemoveDataSink(sink.Key);
					}
				}
			}

			_logger.LogWarning("Payload loop for '{Event}' completed in {Duration}ms", payload.Event, watch.ElapsedMilliseconds);
		}
	}

	public async Task RefreshLock(int guildId, bool locked)
	{
		var payload = new Payload(guildId, "lock", locked.ToString());

		await PayloadChannel.Writer.WriteAsync(payload);
	}

	public ItemDto[] LoadItems()
	{
		return _db.Items
			.Where(x => x.Expansion == Expansion.NoS || x.Expansion == Expansion.LS)
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
			.Where(x => x.Item.Expansion == Expansion.NoS || x.Item.Expansion == Expansion.LS)
			.OrderBy(x => x.Item.Name)
			.Select(x => new LootDto
			{
				ItemId = x.ItemId,
				Name = x.Item.Name,
				RaidQuantity = x.RaidQuantity,
				RotQuantity = x.RotQuantity,
			})
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

	public async Task RefreshItems()
	{
		var items = LoadItems();
		var json = JsonSerializer.Serialize(items, _jsonOptions);
		var payload = new Payload(null, "items", json);

		await PayloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = LoadLoots(guildId);
		var json = JsonSerializer.Serialize(loots, _jsonOptions);
		var payload = new Payload(guildId, "loots", json);

		await PayloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = LoadLootRequests(guildId);
		var json = JsonSerializer.Serialize(requests, _jsonOptions);
		var payload = new Payload(guildId, "requests", json);

		await PayloadChannel.Writer.WriteAsync(payload);
	}

	public async Task DiscordWebhook(HttpClient httpClient, string output, string discordWebhookUrl)
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
				response = await httpClient.PostAsJsonAsync(discordWebhookUrl, json);
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

	public async Task ImportRaidDump(Stream stream, string fileName, int offset)
	{
		var guildId = GetGuildId();
		using var sr = new StreamReader(stream);
		var output = await sr.ReadToEndAsync();
		var nameToClassMap = output
			.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Split('\t'))
			.Where(x => x.Length > 4) // filter out "missing" rows that start with a number, but have nothing after
			.ToDictionary(x => x[1], x => x[3]);

		// create players who do not exist
		var existingNames = _db.Players
			.Where(x => x.GuildId == guildId)
			.Select(x => x.Name)
			.ToArray();
		var players = nameToClassMap.Keys
			.Except(existingNames)
			.Select(x => new Player(x, nameToClassMap[x], guildId))
			.ToList();
		_db.Players.AddRange(players);
		_db.SaveChanges();

		// save raid dumps for all players
		// example filename = RaidRoster_firiona-20220815-205645.txt
		var parts = fileName.Split('-');
		var time = parts[1] + parts[2].Split('.')[0];

		// since the filename of the raid dump doesn't include the timezone, we assume it matches the user's browser UTC offset
		var timestamp = DateTimeOffset
			.ParseExact(time, "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
			.AddMinutes(offset)
			.ToUnixTimeSeconds();

		var raidDumps = _db.Players
			.Where(x => x.GuildId == guildId)
			.Where(x => nameToClassMap.Keys.Contains(x.Name)) // ContainsKey cannot be translated by EFCore
			.Select(x => x.Id)
			.ToArray()
			.Select(x => new RaidDump(timestamp, x))
			.ToArray();
		_db.RaidDumps.AddRange(raidDumps);

		// A unique constraint on the composite index for (Timestamp/Player) will cause exceptions for duplicate raid dumps.
		// It is safe/intended to ignore these exceptions for idempotency.
		try
		{
			_db.SaveChanges();
		}
		catch (DbUpdateException) { }
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
		_logger.LogWarning(nameof(AddDataSink));
	}
}
