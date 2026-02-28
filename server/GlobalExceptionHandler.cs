using Microsoft.AspNetCore.Diagnostics;
using System.Net;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> _logger) : IExceptionHandler
{
	public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		// bots will ping the API but omit a player key - ignore these requests and log as information instead of error
		if (exception is MissingPlayerKeyException)
		{
			_logger.RequiredPlayerKeyMissing();
			httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
			return ValueTask.FromResult(true);
		}

		// if request is GET and token is cancelled, we don't really need to log it

		using var _ = _logger.BeginScope(new LogState
		{
			["IsCancellationRequested"] = cancellationToken.IsCancellationRequested,
		});
		_logger.GlobalExceptionHandler(exception, httpContext.Request.Path);

		return ValueTask.FromResult(false);
	}
}