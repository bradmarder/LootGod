using Microsoft.EntityFrameworkCore;

public class LogMiddleware(
	ILogger<LogMiddleware> _logger,
	LootGodContext _db,
	LootService _service) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		if (context.Request.Method is not ("POST" or "DELETE") || _service.GetPlayerKey() is null)
		{
			await next(context);
			return;
		}

		var playerId = _service.GetPlayerId();
		var player = _db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.Single(x => x.Id == playerId);
		using var _ = _logger.BeginScope(new
		{
			IP = _service.GetIPAddress(),
			Name = player.Name,
			GuildName = player.Guild.Name,
			Path = context.Request.Path.Value,
		});
		_logger.MiddlewareLog(context.Request.Method, context.Request.Path);

		await next(context);
	}
}
