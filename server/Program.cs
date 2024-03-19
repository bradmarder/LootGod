using LootGod;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateSlimBuilder(args);
var adminKey = builder.Configuration["ADMIN_KEY"]!;
var backup = builder.Configuration["BACKUP_URL"]!;
var source = builder.Configuration["DATABASE_URL"]!;

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (o, e) =>
{
	e.Cancel = true;
	cts.Cancel();
};

var useInMemoryDatabase = source is null;
var connString = useInMemoryDatabase
	? new SqliteConnectionStringBuilder
	{
		DataSource = ":memory:",
		Cache = SqliteCacheMode.Shared,
		Mode = SqliteOpenMode.Memory,
	}
	: new SqliteConnectionStringBuilder { DataSource = source };

// required to keep in-memory database alive
using var conn = new SqliteConnection(connString.ConnectionString);
if (useInMemoryDatabase)
{
	conn.Open();
}
else
{
	conn.Dispose();
}

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite(connString.ConnectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LootService>();
builder.Services.AddResponseCompression(x => x.EnableForHttps = true);
builder.Services.AddLogging(x => x
	.ClearProviders()
	.AddSimpleConsole(x =>
	{
		x.SingleLine = true;
		x.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
		x.IncludeScopes = true;
	})
	.SetMinimumLevel(LogLevel.Warning)
	.Configure(y => y.ActivityTrackingOptions = ActivityTrackingOptions.None)
);

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
	rateLimiterOptions.AddFixedWindowLimiter("SingleDailyUsage", x =>
	{
		x.PermitLimit = 1;
		x.QueueLimit = 0;
		x.Window = TimeSpan.FromDays(1);
	});
});

using var app = builder.Build();

await using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<LootGodContext>();

	db.Database.EnsureCreated();

	_ = scope.ServiceProvider
		.GetRequiredService<LootService>()
		.DeliverPayloads();
}

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
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
	OnPrepareResponse = x =>
	{
		x.Context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; child-src 'none';";
		x.Context.Response.Headers.XFrameOptions = "DENY";
		x.Context.Response.Headers["Referrer-Policy"] = "no-referrer";
	}
});
app.UsePathBase("/api");
app.UseMiddleware<LogMiddleware>();
app.MapGet("test", () => "Hello World!").ShortCircuit();

new Endpoints(adminKey, backup).Map(app);

await app.RunAsync(cts.Token);
