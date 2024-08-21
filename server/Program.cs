using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Threading.Channels;

var builder = WebApplication.CreateSlimBuilder(args);
var adminKey = builder.Configuration["ADMIN_KEY"]!;
var source = builder.Configuration["DATABASE_URL"]!;
var useSqliteMemory = builder.Configuration["USE_SQLITE_MEMORY"] == "true";

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (o, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

var connString = useSqliteMemory
	? new SqliteConnectionStringBuilder { Mode = SqliteOpenMode.Memory }
	: new SqliteConnectionStringBuilder { DataSource = source ?? Path.GetTempFileName() };

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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LootService>();
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
	.SetMinimumLevel(LogLevel.Warning)
	.Configure(y => y.ActivityTrackingOptions = ActivityTrackingOptions.None)
);

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();
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
				app.Logger.LogError(ex.Error, context.Request.Path);
			}
			return Task.CompletedTask;
		});
	});
}

app.UseResponseCompression();
app.UseDefaultFiles();

// waiting on .net9 to support serving pre-compressed files (gzip/br)
app.UseStaticFiles(new StaticFileOptions
{
	OnPrepareResponse = x =>
	{
		var headers = x.Context.Response.Headers;
		headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none';";
		headers.XFrameOptions = "DENY";
		headers["Referrer-Policy"] = "no-referrer";
	}
});
app.UsePathBase("/api");
app.UseMiddleware<LogMiddleware>();
app.MapHealthChecks("/healthz");

new Endpoints(adminKey).Map(app);

await app.RunAsync(cts.Token);

public partial class Program { }
