using Microsoft.EntityFrameworkCore;
using System.Net;

public class LogMiddleware(
	ILogger<LogMiddleware> _logger,
	LootGodContext _db,
	LootService _lootService) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		using var _ = _logger.BeginScope(new
		{
			IP = _lootService.GetIPAddress(),
		});

		if (!_lootService.HasPlayerKey())
		{
			await next(context);
			return;
		}

		var player = TryGetPlayer();
		if (player is null)
		{
			// a player key was provided, but it's no longer valid (they were removed from the guild)
			context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
			_logger.RemovedPlayerLogin();
			return;
		}

		using var __ = _logger.BeginScope(new LogState
		{
			["PlayerId"] = player.Id,
			["PlayerName"] = player.Name,
			["GuildId"] = player.GuildId,
			["GuildName"] = player.Guild.Name,
		});

		await next(context);
	}

	private Player? TryGetPlayer()
	{
		try
		{
			var playerId = _lootService.GetPlayerId();

			return _db.Players
				.AsNoTracking()
				.Include(x => x.Guild)
				.Single(x => x.Id == playerId);
		}
		catch
		{
			// no need to log error is thrown - 
			return null;
		}
	}
}