using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace LootGod;

public class LootHub(LootGodContext _db, LootService _service, ILogger<LootHub> _logger) : Hub
{
	public override async Task OnConnectedAsync()
	{
		var key = Guid.Parse(Context.GetHttpContext()!.Request.Query["key"].ToString());
		var player = _db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.FirstOrDefault(x => x.Key == key);

		if (player is not null)
		{
			using var _ = LogContext.PushProperty("IP", _service.GetIPAddress());
			using var __ = LogContext.PushProperty("Name", player.Name);
			using var ___ = LogContext.PushProperty("GuildName", player.Guild.Name);
			_logger.LogInformation(nameof(OnConnectedAsync));

			await Groups.AddToGroupAsync(Context.ConnectionId, player.GuildId.ToString());
		}

		await base.OnConnectedAsync();
	}

	public override Task OnDisconnectedAsync(Exception? exception)
	{
		return base.OnDisconnectedAsync(exception);
	}
}
