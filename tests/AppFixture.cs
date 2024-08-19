using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class AppFixture : IAsyncDisposable
{
	private readonly LootGodApplicationFactory _app = new();

	public HttpClient Client { get; private set; }
	public string AdminKey => Environment.GetEnvironmentVariable("ADMIN_KEY")!;

	static AppFixture()
	{
		Environment.SetEnvironmentVariable("ADMIN_KEY", "TEST");
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
