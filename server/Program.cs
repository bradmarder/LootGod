using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

var builder = WebApplication.CreateSlimBuilder(args);
var adminKey = builder.Configuration["ADMIN_KEY"]!;
var source = builder.Configuration["DATABASE_URL"]!;
var useSqliteMemory = builder.Configuration["USE_SQLITE_MEMORY"] is "true";

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (o, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

var ephemeral = Path.Combine(Path.GetTempPath(), "ephemeral.db");
var connString = useSqliteMemory
	? new SqliteConnectionStringBuilder { Mode = SqliteOpenMode.Memory }
	: new SqliteConnectionStringBuilder { DataSource = source ?? ephemeral };

var healthCheck = builder.Services.AddHealthChecks();
if (useSqliteMemory)
{
	// in-memory database should re-use the same connection
	var conn = new SqliteConnection(connString.ConnectionString);
	conn.Open();
	builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite(conn));
}
else
{
	builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite(connString.ConnectionString));
	healthCheck.AddDbContextCheck<LootGodContext>();
}

if (builder.Environment.IsProduction())
{
	builder.Services
		.AddOpenTelemetry()
		.WithTracing(config => config
			.AddEntityFrameworkCoreInstrumentation(x => x.SetDbStatementForText = true)
			.AddHttpClientInstrumentation()
			.AddAspNetCoreInstrumentation()
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("LootGod"))
			.AddSource(nameof(LootService), nameof(Endpoints), nameof(SyncService), nameof(ImportService))
			.AddOtlpExporter());

	builder.Services
		.AddOptions<AspNetCoreTraceInstrumentationOptions>()
		.Configure<IServiceScopeFactory>((options, factory) =>
		{
			options.EnrichWithHttpRequest = (activity, req) =>
			{
				using var scope = factory.CreateScope();
				var service = scope.ServiceProvider.GetRequiredService<LootService>();
				var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

				activity.SetTag("IP", service.GetIPAddress());
				if (service.GetPlayerKey() is not null)
				{
					try
					{
						activity.SetTag("PlayerId", service.GetPlayerId());
						activity.SetTag("GuildId", service.GetGuildId());
					}
					catch (Exception ex)
					{
						activity.AddException(ex);
						logger.ActivityLoggingError(ex);
					}
				}
			};
		});
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LootService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<LogMiddleware>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(x => Channel.CreateUnbounded<Payload>(new() { SingleReader = true, SingleWriter = false }));
builder.Services.AddSingleton<ConcurrentDictionary<string, DataSink>>();
builder.Services.AddHttpClient<LootService>();
builder.Services.AddHttpClient<SyncService>();
builder.Services.AddHostedService<PayloadDeliveryService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);
builder.Services.AddLogging(x => x
	.ClearProviders()
	.AddSimpleConsole(x =>
	{
		x.IncludeScopes = true;
		x.SingleLine = true;
		x.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
	})
	.AddOpenTelemetry(config =>
	{
		if (builder.Environment.IsProduction())
		{
			config.AddOtlpExporter();
			config.IncludeScopes = true;
		}
	})
	//.Configure(y => y.ActivityTrackingOptions = ActivityTrackingOptions.None)
);

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();

	//try
	//{
	//	db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
	//	db.Database.ExecuteSqlRaw("DELETE FROM ITEMS;");
	//	db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
	//	app.Logger.LogInformation("SYNC RESET");
	//}
	//catch (Exception ex)
	//{
	//	app.Logger.LogError(ex, "SYNC RESET FAIL");
	//}
}

// ensure this comes before app.UseExceptionHandler() so that the GlobalExceptionHandler has access to log state properties
app.UseMiddleware<LogMiddleware>();

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler(_ => { });
}

app.UseResponseCompression();
app.Use(async (context, next) =>
{
	var headers = context.Response.Headers;
	headers.XFrameOptions = "DENY";
	headers["Referrer-Policy"] = "no-referrer";

	// include "img-src 'self' data:;" for bootstrap svgs
	headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none'; img-src 'self' https://items.sodeq.org data:;";

	await next();
});
if (app.Environment.IsProduction())
{
	app.UseDefaultFiles();
	app.MapStaticAssets();
}
app.UsePathBase("/api");
app.MapHealthChecks("/healthz").DisableHttpMetrics();

new Endpoints(adminKey).Map(app);

await app.RunAsync(cts.Token);

public partial class Program { }
