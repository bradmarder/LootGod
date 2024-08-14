using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LogMiddleware(
	RequestDelegate _next,
	ILogger<LogMiddleware> _logger,
	IServiceScopeFactory _scopeFactory)
{
	public async Task InvokeAsync(HttpContext context)
	{
		if (context.Request.Method != "POST")
		{
			await _next(context);
			return;
		}

		await using var scope = _scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();
		var service = scope.ServiceProvider.GetRequiredService<LootService>();

		if (service.GetPlayerKey() is null)
		{
			await _next(context);
			return;
		}

		var playerId = service.GetPlayerId();
		var player = db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.Single(x => x.Id == playerId);
		using var _ = _logger.BeginScope(new
		{
			IP = service.GetIPAddress(),
			Name = player.Name,
			GuildName = player.Guild.Name,
			Path = context.Request.Path.Value,
		});
		_logger.LogWarning("POST " + context.Request.Path);

		await _next(context);
	}
}
