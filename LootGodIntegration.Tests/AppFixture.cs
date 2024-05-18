using Microsoft.AspNetCore.Mvc.Testing;

namespace LootGodIntegration.Tests;

public class AppFixture : IAsyncDisposable
{
	private readonly WebApplicationFactory<Program> _app = new();
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
