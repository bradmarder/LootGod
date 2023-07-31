using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootService
{
	private readonly ILogger _logger;
	private readonly LootGodContext _db;
	private readonly IHubContext<LootHub> _hub;
	private readonly IHttpContextAccessor _httpContextAccessor;

	public LootService(ILogger<LootService> logger, LootGodContext db, IHubContext<LootHub> hub, IHttpContextAccessor httpContextAccessor)
	{
		_logger = logger;
		_db = db;
		_hub = hub;
		_httpContextAccessor = httpContextAccessor;
	}

	public Guid? GetPlayerKey() =>
		_httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Player-Key", out var headerKey) ? Guid.Parse(headerKey.ToString())
		: _httpContextAccessor.HttpContext!.Request.Query.TryGetValue("playerKey", out var queryKey) ? Guid.Parse(queryKey.ToString())
		: null;

	public string? GetIPAddress() =>
		_httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Fly-Client-IP", out var val)
			? val
			: _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();

	public async Task<int> GetPlayerId()
	{
		var key = GetPlayerKey();

		return await _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active != false)
			.Select(x => x.Id)
			.FirstOrDefaultAsync();
	}

	public async Task<int> GetGuildId()
	{
		var key = GetPlayerKey();

		return await _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active != false)
			.Select(x => x.GuildId)
			.FirstOrDefaultAsync();
	}

	public async Task<bool> GetAdminStatus()
	{
		var key = GetPlayerKey();

		return await _db.Players
			.Where(x => x.Key == key)
			.Where(x => x.Active == true)
			.Select(x => x.Admin)
			.FirstOrDefaultAsync();
	}

	public async Task<bool> IsGuildLeader()
	{
		var key = GetPlayerKey();

		return await _db.Players.AnyAsync(x => x.Key == key && x.Rank!.Name == "Leader");
	}

	public async Task<bool> GetRaidLootLock()
	{
		var key = GetPlayerKey();

		return await _db.Players
			.Where(x => x.Key == key)
			.Select(x => x.Guild.RaidLootLocked)
			.SingleAsync();
	}

	public async Task EnsureAdminStatus()
	{
		if (!await GetAdminStatus())
		{
			throw new UnauthorizedAccessException(GetPlayerKey().ToString());
		}
	}

	public async Task EnsureGuildLeader()
	{
		if (!await IsGuildLeader())
		{
			throw new UnauthorizedAccessException(GetPlayerKey().ToString());
		}
	}

	public async Task EnsureRaidLootUnlocked()
	{
		if (await GetRaidLootLock())
		{
			throw new Exception(GetPlayerKey().ToString());
		}
	}

	public async Task<string> GetGrantedLootOutput()
	{
		var guildId = await GetGuildId();

		var items = (await _db.LootRequests
			.AsNoTracking()
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

		return string.Join(Environment.NewLine, items);
	}

	public async Task RefreshLock(int guildId, bool locked)
	{
		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("lock", locked);
	}

	public async Task<LootDto[]> LoadLoots(int guildId)
	{
		return await _db.Loots
			.Where(x => x.GuildId == guildId)
			.Where(x => x.Expansion == Expansion.ToL || x.Expansion == Expansion.NoS)
			.OrderBy(x => x.Name)
			.Select(x => new LootDto
			{
				Id = x.Id,
				Name = x.Name,
				RaidQuantity = x.RaidQuantity,
				RotQuantity = x.RotQuantity,
			})
			.ToArrayAsync();
	}

	public async Task<LootRequestDto[]> LoadLootRequests(int guildId)
	{
		return await _db.LootRequests
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
				Quantity = x.Quantity,
				RaidNight = x.RaidNight,
				IsAlt = x.IsAlt,
				Granted = x.Granted,
				CurrentItem = x.CurrentItem,
			})
			.ToArrayAsync();
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = await LoadLoots(guildId);

		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("loots", loots);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = await LoadLootRequests(guildId);

		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("requests", requests);
	}

	public async Task DiscordWebhook(HttpClient httpClient, string output, string discordWebhookUrl)
	{
		// A single bucket must be under the 2k max for discord (excludes backticks/newlines/emojis?)
		// Assume 1_700 max characters per bucket to safely account for splitting lines evenly
		var bucketCount = Math.Round(output.Length / 1_700d, MidpointRounding.ToPositiveInfinity);
		var lines = output.Split(Environment.NewLine);
		var maxLinesPerBucket = (int)Math.Round(lines.Length / bucketCount, MidpointRounding.ToPositiveInfinity);
		var buckets = lines.Chunk(maxLinesPerBucket);

		foreach (var bucket in buckets)
		{
			var data = string.Join(Environment.NewLine, bucket);
			var json = new { content = $"```{Environment.NewLine}{data}{Environment.NewLine}```" };
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
