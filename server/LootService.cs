using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootService
{
	private readonly LootGodContext _db;
	private readonly IHubContext<LootHub> _hub;
	private readonly IHttpContextAccessor _httpContextAccessor;

	public LootService(LootGodContext db, IHubContext<LootHub> hub, IHttpContextAccessor httpContextAccessor)
	{
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
			.FirstOrDefaultAsync();
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
				Quantity = x.RaidQuantity,
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
}