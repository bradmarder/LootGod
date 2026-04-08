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

		using var _ = await _semaphore.LockAsync();

		await next(context);
	}
}
