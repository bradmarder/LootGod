public class SingleConnectionMiddleware(SemaphoreSlim _semaphore) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		// special exemption for this endpoint because it handles its own connection management
		if (context.Request.Path.Value!.Contains("/sse", StringComparison.OrdinalIgnoreCase))
		{
			await next(context);
			return;
		}

		await _semaphore.WaitAsync();
		try
		{
			await next(context);
		}
		finally
		{
			_semaphore.Release();
		}
	}
}
