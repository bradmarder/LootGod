using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace LootGod;

public class LootService(ILogger<LootService> _logger, LootGodContext _db, IHttpContextAccessor _httpContextAccessor)
{
	private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private static readonly Channel<string> PayloadChannel = Channel.CreateUnbounded<string>(new() { SingleReader = true, SingleWriter = false });
	private static readonly ConcurrentDictionary<string, (int GuildId, HttpResponse Response)> Payloads = new();

	//var info = System.Text.Json.Serialization.Metadata.JsonTypeInfo.CreateJsonTypeInfo<string>(new JsonSerializerOptions());
	//await JsonSerializer.SerializeAsync(ctx.Response.Body, item, typeof(LootDto), SourceGenerationContext.Default, ct);

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
			.FirstOrDefault();
	}

	public int GetGuildId()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active != false)
			.Select(x => x.GuildId)
			.FirstOrDefault();
	}

	public bool GetAdminStatus()
	{
		var key = GetPlayerKey();

		return _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active == true)
			.Select(x => x.Admin)
			.FirstOrDefault();
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
			.Include(x => x.Loot)
			.Include(x => x.Player)
			.Where(x => x.Player.GuildId == guildId)
			.Where(x => x.Granted && !x.Archived)
			.OrderBy(x => x.LootId)
			.ThenBy(x => x.AltName ?? x.Player.Name)
			.ToList()
			.GroupBy(x => (x.LootId, x.AltName ?? x.Player.Name))
			.Select(x =>
			{
				var request = x.First();
				return new LootOutput(request.Loot.Name, request.AltName ?? request.Player.Name, x.Sum(y => y.Quantity));
			})
			.ToArray();

		var rotLoot = _db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.RaidQuantity > 0)
			.Select(x => new
			{
				x.Name,
				Quantity = x.RaidQuantity - x.LootRequests.Count(x => x.Granted && !x.Archived),
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

	public record LootOutput(string Loot, string Name, int Quantity);

	public void AddPayloadConnection(string connectionId, int guildId, HttpResponse response)
	{
		Payloads.TryAdd(connectionId, (guildId, response));

		var key = GetPlayerKey();
		var player = _db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.FirstOrDefault(x => x.Key == key);

		if (player is not null)
		{
			using var _ = _logger.BeginScope(new
			{ 
				IP = GetIPAddress(),
				Name = player.Name,
				GuildName = player.Guild.Name,
			});
			_logger.LogWarning(nameof(AddPayloadConnection));
		}
	}

	public void RemovePayloadConnection(string connectionId)
	{
		_logger.LogWarning("REMOVE PAYLOAD " + connectionId);
		Payloads.Remove(connectionId, out _);
	}

	public async Task DeliverPayloads()
	{
		await foreach (var data in PayloadChannel.Reader.ReadAllAsync())
		{
			var chunks = data.Split('|');
			var guildId = int.Parse(chunks[0]);
			var start = DateTime.UtcNow;

			foreach (var payload in Payloads)
			{
				if (payload.Value.GuildId == guildId)
				{
					try
					{
						//await payload.Response.WriteAsync($"id: {id++}\n", ct);
						await payload.Value.Response.WriteAsync($"event: {chunks[1]}\n");
						await payload.Value.Response.WriteAsync($"data: {chunks[2]}\n");
						await payload.Value.Response.WriteAsync("\n\n");
						await payload.Value.Response.Body.FlushAsync();
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Orphan connection removed");
						RemovePayloadConnection(payload.Key);
					}
				}
			}

			var duration = DateTime.UtcNow - start;
			_logger.LogWarning($"Payload loop for '{chunks[1]}' completed in {(long)duration.TotalMilliseconds}ms");
		}
	}

	public async Task RefreshLock(int guildId, bool locked)
	{
		await PayloadChannel.Writer.WriteAsync(guildId + "|lock|" + locked.ToString());
	}

	public LootDto[] LoadLoots(int guildId)
	{
		return _db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.Expansion == Expansion.NoS || x.Expansion == Expansion.LS)
			.OrderBy(x => x.Name)
			.Select(x => new LootDto
			{
				Id = x.Id,
				Name = x.Name,
				RaidQuantity = x.RaidQuantity,
				RotQuantity = x.RotQuantity,
			})
			.ToArray();
	}

	public LootRequestDto[] LoadLootRequests(int guildId)
	{
		return _db.LootRequests
			.Where(x => x.Player.GuildId == guildId)
			.Where(x => !x.Archived)
			.OrderByDescending(x => x.Spell != null)
			.ThenBy(x => x.LootId)
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
				LootId = x.LootId,
				LootName = x.Loot.Name,
				Quantity = x.Quantity,
				RaidNight = x.RaidNight,
				IsAlt = x.IsAlt,
				Granted = x.Granted,
				CurrentItem = x.CurrentItem,
			})
			.ToArray();
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = LoadLoots(guildId);
		var json = JsonSerializer.Serialize(loots, _jsonOptions);
		await PayloadChannel.Writer.WriteAsync(guildId + "|loots|" + json);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = LoadLootRequests(guildId);
		var json = JsonSerializer.Serialize(requests, _jsonOptions);
		await PayloadChannel.Writer.WriteAsync(guildId + "|requests|" + json);
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
}
