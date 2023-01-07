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

	private Guid? GetPlayerKey() =>
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
			.Select(x => x.Id)
			.FirstOrDefaultAsync();
	}

	public async Task<int> GetGuildId()
	{
		var key = GetPlayerKey();

		return await _db.Players
			.Where(x => x.Key == key)
			.Select(x => x.GuildId)
			.FirstOrDefaultAsync();
	}

	public async Task RefreshLock(bool locked)
	{
		await _hub.Clients.All.SendAsync("lock", locked);
	}

	public async Task RefreshLoots()
	{
		var loots = (await _db.Loots
			.Where(x => x.Expansion == Expansion.ToL)
			.OrderBy(x => x.Name)
			.ToListAsync())
			.Select(x => new LootDto(x))
			.ToArray();

		await _hub.Clients.All.SendAsync("loots", loots);
	}

	public async Task RefreshRequests()
	{
		var requests = (await _db.LootRequests
			.Include(x => x.Player)
			.Where(x => !x.Archived)
			.OrderByDescending(x => x.Spell)
			.ThenBy(x => x.LootId)
			.ThenByDescending(x => x.AltName ?? x.Player.Name)
			.ToListAsync())
			.Select(x => new LootRequestDto(x))
			.ToArray();

		await _hub.Clients.All.SendAsync("requests", requests);
	}
}