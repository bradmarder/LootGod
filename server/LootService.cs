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
		_httpContextAccessor.HttpContext!.Request.Headers.TryGetValue("Player-Key", out var val)
			? Guid.Parse(val.ToString())
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

	public async Task EnsureRaidLootUnlocked()
	{
		if (await GetRaidLootLock())
		{
			throw new Exception(GetPlayerKey().ToString());
		}
	}

	public async Task RefreshLock(int guildId, bool locked)
	{
		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("lock", locked);
	}

	public async Task RefreshLoots(int guildId)
	{
		var loots = (await _db.Loots
			.AsNoTracking()
			.Where(x => x.GuildId == guildId)
			.Where(x => x.Expansion == Expansion.ToL || x.Expansion == Expansion.NoS)
			.OrderBy(x => x.Name)
			.ToListAsync())
			.Select(x => new LootDto(x))
			.ToArray();

		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("loots", loots);
	}

	public async Task RefreshRequests(int guildId)
	{
		var requests = (await _db.LootRequests
			.AsNoTracking()
			.Include(x => x.Player)
			.Where(x => x.Player.GuildId == guildId)
			.Where(x => !x.Archived)
			.OrderByDescending(x => x.Spell)
			.ThenBy(x => x.LootId)
			.ThenByDescending(x => x.AltName ?? x.Player.Name)
			.ToListAsync())
			.Select(x => new LootRequestDto(x))
			.ToArray();

		await _hub.Clients
			.Group(guildId.ToString())
			.SendAsync("requests", requests);
	}
}