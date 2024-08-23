using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class AppFixture : IAsyncDisposable
{
	public const string AdminKey = "TEST";

	private readonly LootGodApplicationFactory _app = new();

	public HttpClient Client { get; private set; }

	static AppFixture()
	{
		Environment.SetEnvironmentVariable("ADMIN_KEY", AdminKey);
		Environment.SetEnvironmentVariable("USE_SQLITE_MEMORY", "true");
	}

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
			x.AddSingleton<TimeProvider, FixedTimeProvider>();
		});
	}
}

public class FixedTimeProvider : TimeProvider
{
	public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(1721678244259);
}
