using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LootGodIntegration.Tests;

public class AppFixture : IAsyncDisposable
{
	private readonly LootGodApplicationFactory _app = new();
	private readonly IConfiguration _config;

	public HttpClient Client { get; private set; }
	public string AdminKey => _config.GetValue<string>("ADMIN_KEY") ?? throw new Exception("Missing ADMIN_KEY");

	public AppFixture()
	{
		Client = _app.CreateDefaultClient();
		_config = _app.Services.GetRequiredService<IConfiguration>();
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
			x.AddSingleton<TimeProvider, FixedTimeProvider>();
		});
	}
}

public class FixedTimeProvider : TimeProvider
{
	public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(1721678244259);
}
