using Microsoft.AspNetCore.Diagnostics;
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
			.AddEntityFrameworkCoreInstrumentation(options =>
			{
				options.SetDbStatementForText = true;
				options.EnrichWithIDbCommand = (activity, db) =>
				{
					foreach (System.Data.IDataParameter x in db.Parameters)
					{
						//if (x.Value is System.Collections.IEnumerable) { continue; }
						//activity.SetTag("Parameter.Value." + x.ParameterName, x.Value);
						//activity.SetTag("Parameter.DbType." + x.ParameterName, x.DbType);
					}
					//activity.SetTag("Parameters", parameters);
				};
			})
			.AddHttpClientInstrumentation(options =>
			{
				options.FilterHttpRequestMessage = req => req.RequestUri?.Host is not "api.honeycomb.io"; // TODO:
			})
			.AddAspNetCoreInstrumentation()
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("LootGod"))
			.AddSource(nameof(LootService), nameof(Endpoints))
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
						logger.LogError(ex, null);
					}
				}
			};
		});
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LootService>();
builder.Services.AddScoped<LogMiddleware>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(x => Channel.CreateUnbounded<Payload>(new() { SingleReader = true, SingleWriter = false }));
builder.Services.AddSingleton<ConcurrentDictionary<string, DataSink>>();
builder.Services.AddHttpClient<LootService>();
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
	.Configure(y => y.ActivityTrackingOptions = ActivityTrackingOptions.None)
);

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();

	// update all loot xpac, set to new xpac if created in last 2 weeks
	//var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeMilliseconds();
	//db.Items
	//	.Where(x => x.CreatedDate > twoWeeksAgo)
	//	.ExecuteUpdate(x => x.SetProperty(y => y.Expansion, Expansion.ToB));
}

if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler(opt =>
	{
		opt.Run(context =>
		{
			var ex = context.Features.Get<IExceptionHandlerFeature>();
			if (ex is not null)
			{
				app.Logger.LogError(ex.Error, "UseExceptionHandler - {RequestPath}", context.Request.Path);
			}
			return Task.CompletedTask;
		});
	});
}

app.UseResponseCompression();
app.Use(async (context, next) =>
{
	var headers = context.Response.Headers;
	headers.XFrameOptions = "DENY";
	headers["Referrer-Policy"] = "no-referrer";

	// include "img-src 'self' data:;" for bootstrap svgs
	headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none'; img-src 'self' data:;";

	await next();
});
if (app.Environment.IsProduction())
{
	app.UseDefaultFiles();
	app.MapStaticAssets();
}
app.UsePathBase("/api");
app.UseMiddleware<LogMiddleware>();
app.MapHealthChecks("/healthz").DisableHttpMetrics();

new Endpoints(adminKey).Map(app);

await app.RunAsync(cts.Token);

public partial class Program { }
