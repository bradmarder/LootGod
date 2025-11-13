using Microsoft.EntityFrameworkCore;

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

		if (_lootService.GetPlayerKey() is null)
		{
			await next(context);
			return;
		}

		var player = TryGetPlayer();
		using var __ = _logger.BeginScope(new
		{
			PlayerId = player?.Id,
			PlayerName = player?.Name,
			GuildId = player?.GuildId,
			GuildName = player?.Guild.Name,
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
			// no need to log if any error is thrown - the pipeline will throw the same error later to be caught by the GlobalExceptionHandler
			return null;
		}
	}
}
