using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
var useSqliteMemory = builder.Configuration["USE_SQLITE_MEMORY"] == bool.TrueString;

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

	builder.Services.AddScoped<SingleConnectionMiddleware>();
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

	builder.Services.AddOptions<AspNetCoreTraceInstrumentationOptions>();
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(x => new SemaphoreSlim(1, 1)); // only intended for SQLite single connection
builder.Services.AddScoped<LootService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<LogMiddleware>();
//builder.Services.AddScoped<AntiForgeryTokenValidationMiddleware>();
builder.Services.AddSingleton(TimeProvider.System);

var channel = Channel.CreateUnbounded<Payload>(new() { SingleReader = true, SingleWriter = false });
builder.Services.AddSingleton(x => channel.Reader);
builder.Services.AddSingleton(x => channel.Writer);

builder.Services.AddSingleton<ConcurrentDictionary<string, DataSink>>();
builder.Services.AddHttpClient<LootService>();
builder.Services.AddHttpClient<SyncService>();
builder.Services.AddHostedService<PayloadDeliveryService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);
//builder.Services.AddAntiforgery(options =>
//{
//	options.Cookie.HttpOnly = true;
//	options.Cookie.SameSite = SameSiteMode.Strict;
//	options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
//});
builder.Services.AddLogging(x => x
	.ClearProviders()
	.AddJsonConsole(x =>
	{
		x.IncludeScopes = true;
		x.UseUtcTimestamp = true;
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

// Explicitly enable HTTPS configuration for Kestrel because we are using CreateSlimBuilder
// builder.WebHost.UseKestrelHttpsConfiguration();

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();

	//try
	//{
	//	db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
	//	db.Database.ExecuteSqlRaw("DROP TABLE Items");
	//	db.Database.ExecuteSqlRaw("DROP TABLE Spells");
	//	var sync = scope.ServiceProvider.GetRequiredService<SyncService>();
	//	await sync.DataSync(CancellationToken.None);
	//	db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
	//	app.Logger.LogInformation("SYNC 3 SUCCESS");
	//}
	//catch (Exception ex)
	//{
	//	app.Logger.LogError(ex, "SYNC 3 FAIL");
	//}
}

if (useSqliteMemory)
{
	// ensure this comes before any middleware that access the shared SQLite connection
	app.UseMiddleware<SingleConnectionMiddleware>();
}

// ensure this comes before app.UseExceptionHandler() so that the GlobalExceptionHandler has access to log state properties
app.UseMiddleware<LogMiddleware>();

app.UseExceptionHandler(_ => { });
if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
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

// people have extensions that block cookies which breaks this feature
//app.UseAntiforgery();
//app.UseMiddleware<AntiForgeryTokenValidationMiddleware>();

new Endpoints(adminKey).Map(app);

using (app.Logger.BeginScope(new
{
	Machine = Environment.MachineName,
	Application = app.Environment.ApplicationName,
	Environment = app.Environment.EnvironmentName,
	Database = useSqliteMemory ? null : source ?? ephemeral,
	SqliteInMemory = useSqliteMemory,
}))
{
	app.Lifetime.ApplicationStarted.Register(app.Logger.ApplicationStarted);
	app.Lifetime.ApplicationStopping.Register(() => app.Logger.ApplicationStopping(cts.Token.IsCancellationRequested));
	app.Lifetime.ApplicationStopped.Register(() => app.Logger.ApplicationStopped(cts.Token.IsCancellationRequested));
}

await app.RunAsync(cts.Token);

public partial class Program { }
