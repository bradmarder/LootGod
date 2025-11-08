using Microsoft.AspNetCore.Diagnostics;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> _logger) : IExceptionHandler
{
	public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		// if request is GET and token is cancelled, we don't really need to log it

		using var _ = _logger.BeginScope(new
		{
			cancellationToken.IsCancellationRequested,
		});
		_logger.GlobalExceptionHandler(exception, httpContext.Request.Path);

		return ValueTask.FromResult(false);
	}
}
