using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace LootGod;

public class LogMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger _logger;
	private readonly IServiceScopeFactory _scopeFactory;

	public LogMiddleware(RequestDelegate next, ILogger<LogMiddleware> logger, IServiceScopeFactory scopeFactory)
	{
		_next = next;
		_logger = logger;
		_scopeFactory = scopeFactory;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();
		var service = scope.ServiceProvider.GetRequiredService<LootService>();
		
		if (context.Request.Method != "POST" || service.GetPlayerKey() is null)
		{
			await _next(context);
			return;
		}

		var playerId = service.GetPlayerId();
		var player = db.Players
			.AsNoTracking()
			.Include(x => x.Guild)
			.Single(x => x.Id == playerId);
		using var _ = LogContext.PushProperty("IP", service.GetIPAddress());
		using var __ = LogContext.PushProperty("Name", player.Name);
		using var ___ = LogContext.PushProperty("GuildName", player.Guild.Name);
		using var ____ = LogContext.PushProperty("Path", context.Request.Path.Value);
		_logger.LogInformation("POST " + context.Request.Path);

		await _next(context);
	}
}
