using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace LootGod;

public class LootHub : Hub
{
	private readonly LootGodContext _db;
	private readonly LootService _service;
	private readonly ILogger _logger;

	public LootHub(LootGodContext db, LootService service, ILogger<LootHub> logger)
	{
		_db = db;
		_service = service;
		_logger = logger;
	}

	public override async Task OnConnectedAsync()
	{
		var key = Guid.Parse(Context.GetHttpContext()!.Request.Query["key"].ToString());
		var player = await _db.Players
			.Include(x => x.Guild)
			.FirstOrDefaultAsync(x => x.Key == key);

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
