using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;
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
	private static readonly Expansion[] CurrentExpansions = [Expansion.ToB, Expansion.SoR];

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
			.Where(x => x.Guest || x.Active != false)
			.Single(x => x.Key == key)
			.Id;
	}

	public int GetGuildId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Guest || x.Active != false)
			.Single(x => x.Key == key)
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
			.Where(x => x.Granted && x.Archived == null)
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
				Quantity = x.RaidQuantity - x.Item.LootRequests.Count(x => x.Player.GuildId == guildId && x.Granted && x.Archived == null && x.RaidNight == raidNight),
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

	public bool AddDataSink(string connectionId, HttpContext context)
	{
		LogNewDataSink();

		var guildId = GetGuildId();
		var sink = new DataSink
		{
			GuildId = guildId,
			Context = context,
		};
		return _dataSinks.TryAdd(connectionId, sink);
	}

	public bool RemoveDataSink(string connectionId) => _dataSinks.Remove(connectionId, out _);

	public ItemSearch[] LoadItems()
	{
		return _db.Items
			.Where(x => EF.Constant(CurrentExpansions).Contains(x.Expansion))
			.OrderBy(x => x.Name)
			.Select(x => new ItemSearch { Id = x.Id, Name = x.Name })
			.ToArray();
	}

	public LootDto[] LoadLoots(int guildId)
	{
		return _db.Loots
			.Where(x => x.GuildId == EF.Constant(guildId))
			.Where(x => EF.Constant(CurrentExpansions).Contains(x.Item.Expansion))
			.ProjectToDto()
			.OrderBy(x => x.Item.Name)
			.ToArray();
	}

	public LootRequestDto[] LoadLootRequests(int guildId)
	{
		return _db.LootRequests
			.Where(x => x.Player.GuildId == EF.Constant(guildId))
			.Where(x => x.Archived == null)
			.OrderByDescending(x => x.Spell != null)
			.ThenBy(x => x.ItemId)
			.ThenByDescending(x => x.AltName ?? x.Player.Name)
			.ProjectToDto()
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
		var json = JsonSerializer.Serialize(items, AppJsonSerializerContext.Default.ItemSearchArray);
		var payload = new Payload(null, "items", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = LoadLoots(guildId);
		var json = JsonSerializer.Serialize(loots, AppJsonSerializerContext.Default.LootDtoArray);
		var payload = new Payload(guildId, "loots", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = LoadLootRequests(guildId);
		var json = JsonSerializer.Serialize(requests, AppJsonSerializerContext.Default.LootRequestDtoArray);
		var payload = new Payload(guildId, "requests", json);

		await _payloadChannel.Writer.WriteAsync(payload);
	}

	private static IEnumerable<string[]> GetBuckets(string output)
	{
		// A single bucket must be under the 2k max for discord (excludes backticks/newlines/emojis?)
		// Assume 1_700 max characters per bucket to safely account for splitting lines evenly
		var bucketCount = Math.Round(output.Length / 1_700d, MidpointRounding.ToPositiveInfinity);
		var lines = output.Split(Environment.NewLine);
		var maxLinesPerBucket = (int)Math.Round(lines.Length / bucketCount, MidpointRounding.ToPositiveInfinity);

		return lines.Chunk(maxLinesPerBucket);
	}

	public async Task DiscordWebhook(string output, string discordWebhookUrl)
	{
		const string syntax = "coq";

		foreach (var bucket in GetBuckets(output))
		{
			var data = string.Join(Environment.NewLine, bucket);
			var json = new DiscordWebhookContent($"```{syntax}{Environment.NewLine}{data}{Environment.NewLine}```");
			using var _ = _logger.BeginScope(new
			{
				Data = data,
				DiscordWebhookUrl = discordWebhookUrl,
			});
			HttpResponseMessage? response = null;
			try
			{
				response = await _httpClient.PostAsJsonAsync(discordWebhookUrl, json, AppJsonSerializerContext.Default.DiscordWebhookContent);
				response.EnsureSuccessStatusCode();
				_logger.DiscordWebhookSuccess();
			}
			catch (Exception ex)
			{
				var content = await TryReadContentAsync(response);
				var state = new { ResponseContent = content };
				using (_logger.BeginScope(state))
				{
					_logger.DiscordWebhookFailure(ex);
				}
			}
			finally
			{
				response?.Dispose();
			}
		}
	}

	private async Task<string> TryReadContentAsync(HttpResponseMessage? response)
	{
		try
		{
			return response is null ? "" : await response.Content.ReadAsStringAsync();
		}
		catch (Exception ex)
		{
			_logger.DiscordReadContentError(ex);
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
		_logger.DataSinkCreated();
	}
}
