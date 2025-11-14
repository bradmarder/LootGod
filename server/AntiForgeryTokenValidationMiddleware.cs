using Microsoft.AspNetCore.Antiforgery;

public class AntiForgeryTokenValidationMiddleware(IAntiforgery _antiforgery) : IMiddleware
{
	public async Task InvokeAsync(HttpContext context, RequestDelegate next)
	{
		if (HttpMethods.IsPost(context.Request.Method))
		{
			await _antiforgery.ValidateRequestAsync(context);
		}

		await next(context);
	}
}
