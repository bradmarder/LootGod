using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LootGodIntegration.Tests;

public class AppFixture : IAsyncDisposable
{
	private readonly LootGodApplicationFactory _app = new();
	public HttpClient Client { get; private set; }

	public AppFixture()
	{
		Client = _app.CreateDefaultClient();
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync();
	}
}

public class LootGodApplicationFactory : WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(x =>
		{
			x.AddSingleton<FixedTimeProvider>();
		});
	}
}

public class FixedTimeProvider : TimeProvider
{
	public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(1721678244259);
}
