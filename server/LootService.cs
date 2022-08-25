using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootService
{
	private readonly LootGodContext _db;
	private readonly IHubContext<LootHub> _hub;

	public LootService(LootGodContext db, IHubContext<LootHub> hub)
	{
		_db = db;
		_hub = hub;
	}

	public async Task RefreshLock(bool locked)
	{
		await _hub.Clients.All.SendAsync("lock", locked);
	}

	public async Task RefreshLoots()
	{
		var loots = (await _db.Loots
			.OrderBy(x => x.Name)
			.ToListAsync())
			.Select(x => new LootDto(x))
			.ToArray();

		await _hub.Clients.All.SendAsync("loots", loots);
	}

	public async Task RefreshRequests()
	{
		var requests = (await _db.LootRequests
			.Where(x => !x.Archived)
			.OrderByDescending(x => x.Spell)
			.ThenBy(x => x.LootId)
			.ThenByDescending(x => x.CharacterName)
			.ToListAsync())
			.Select(x => new LootRequestDto(x))
			.ToArray();

		await _hub.Clients.All.SendAsync("requests", requests);
	}
}