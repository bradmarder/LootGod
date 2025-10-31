using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class AppFixture : IAsyncDisposable
{
	public const string AdminKey = "TEST";
	public static readonly DateTimeOffset DefaultNow = DateTimeOffset.FromUnixTimeMilliseconds(1721678244259);

	private readonly WebApplicationFactory<Program> _app;

	public HttpClient Client { get; private set; }

	static AppFixture()
	{
		Environment.SetEnvironmentVariable("ADMIN_KEY", AdminKey);
		Environment.SetEnvironmentVariable("USE_SQLITE_MEMORY", "true");
	}

	public AppFixture(double futureDays = 0)
	{
		var now = DefaultNow.AddDays(futureDays);

		_app = new LootGodApplicationFactory(now);
		Client = _app.CreateDefaultClient();
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync();
	}
}

public class LootGodApplicationFactory(DateTimeOffset now) : WebApplicationFactory<Program>
{
	private static HttpResponseMessage HandlerFunc(HttpRequestMessage msg)
	{
		// the spellDataUrl has a `:` in the path which is automatically converted to '_' when downloading
		var file = msg.RequestUri!.AbsolutePath
			.Split('/')
			.Last()
			.Replace(':', '_');
		var path = Path.Combine(AppContext.BaseDirectory, file);
		var stream = File.OpenRead(path);

		return new()
		{
			Content = new StreamContent(stream)
		};
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(x =>
		{
			x.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
			x.AddLogging(y => y.ClearProviders());
			x.AddHttpClient<SyncService>().ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler(HandlerFunc));
		});
	}
}

public class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
	public override DateTimeOffset GetUtcNow() => now;
}

public class FakeHttpMessageHandler : DelegatingHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _handlerFunc;

	public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
	{
		_handlerFunc = handlerFunc;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return Task.FromResult(_handlerFunc(request));
	}
}