using Microsoft.EntityFrameworkCore;

public class LogMiddleware(
	ILogger<LogMiddleware> _logger,
	LootGodContext _db,
	LootService _service) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		using var _ = _logger.BeginScope(new
		{
			IP = _service.GetIPAddress(),
		});

		if (context.Request.Method is not ("POST" or "DELETE") || _service.GetPlayerKey() is null)
		{
			await next(context);
			return;
		}

		try
		{
			var playerId = _service.GetPlayerId();
			var player = _db.Players
				.AsNoTracking()
				.Include(x => x.Guild)
				.Single(x => x.Id == playerId);
			using var __ = _logger.BeginScope(new
			{
				PlayerId = playerId,
				PlayerName = player.Name,
				GuildId = player.GuildId,
				GuildName = player.Guild.Name,
			});
		}
		catch { } // no need to log if any error is thrown - the pipeline will throw the same error later to be caught by the GlobalExceptionHandler

		await next(context);
	}
}
