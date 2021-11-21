using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootHub : Hub
{
	public static async Task RefreshLoots(HttpContext context)
	{
		var db = context.RequestServices.GetRequiredService<LootGodContext>();
		var loots = await db.Loots.OrderBy(x => x.Name).ToListAsync();
		var requests = await db.LootRequests
			.OrderByDescending(x => x.Spell)
			.ThenBy(x => x.LootId)
			.ThenByDescending(x => x.CharacterName)
			.ToListAsync();
		var lootLock = await db.LootLocks.OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync();

		var hub = context.RequestServices.GetRequiredService<IHubContext<LootHub>>();
		await hub.Clients.All.SendAsync("refresh",
			lootLock?.Lock ?? false,
			loots.Select(x => new LootDto(x)),
			requests.Select(x => new LootRequestDto(x)));
	}
}
