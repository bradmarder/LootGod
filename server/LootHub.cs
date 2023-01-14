using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootHub : Hub
{
	private readonly LootGodContext _db;

	public LootHub(LootGodContext db)
	{
		_db = db;
	}

	public override async Task OnConnectedAsync()
	{
		var key = Guid.Parse(Context.GetHttpContext()!.Request.Query["key"].ToString());
		var player = await _db.Players.FirstOrDefaultAsync(x => x.Key == key);
		await Groups.AddToGroupAsync(Context.ConnectionId, player!.GuildId.ToString());
		await base.OnConnectedAsync();
	}
}
