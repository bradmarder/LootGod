public static partial class Log
{
	[LoggerMessage(1, LogLevel.Information, "Spell sync {SpellCount} of {TotalSpellCount} in {ElapsedMs}")]
	public static partial void SpellSyncSuccess(this ILogger logger, int spellCount, int totalSpellCount, long elapsedMs);

	[LoggerMessage(2, LogLevel.Information, "Item sync {RaidItemCount} of {TotalItemCount} in {ElapsedMs}")]
	public static partial void ItemSyncSuccess(this ILogger logger, int raidItemCount, int totalItemCount, long elapsedMs);

	[LoggerMessage(3, LogLevel.Information, "Loot requests finished")]
	public static partial void LootRequestsFinished(this ILogger logger);

	[LoggerMessage(4, LogLevel.Information, "DataSink created")]
	public static partial void DataSinkCreated(this ILogger logger);

	[LoggerMessage(5, LogLevel.Information, "DataSink removed after {Elapsed}s")]
	public static partial void DataSinkRemoved(this ILogger logger, int elapsed);

	[LoggerMessage(6, LogLevel.Information, "Loot request created")]
	public static partial void LootRequestCreated(this ILogger logger);

	[LoggerMessage(7, LogLevel.Information, "Loot request deleted")]
	public static partial void LootRequestDeleted(this ILogger logger);

	[LoggerMessage(8, LogLevel.Information, "Loot request granted - {LootRequestId} - {Granted}")]
	public static partial void LootRequestGranted(this ILogger logger, int lootRequestId, bool granted);

	[LoggerMessage(9, LogLevel.Error, "Discord read content failed")]
	public static partial void DiscordReadContentError(this ILogger logger, Exception ex);

	[LoggerMessage(10, LogLevel.Warning, "Import dump warning - `{Message}`")]
	public static partial void ImportDumpWarning(this ILogger logger, string message);

	[LoggerMessage(11, LogLevel.Error, "GlobalExceptionHandler - {RequestPath}")]
	public static partial void GlobalExceptionHandler(this ILogger logger, Exception ex, string requestPath);

	[LoggerMessage(12, LogLevel.Information, "Raid dump import completed")]
	public static partial void RaidDumpImportCompleted(this ILogger logger);

	[LoggerMessage(13, LogLevel.Information, "Guild dump import completed")]
	public static partial void GuildDumpImportCompleted(this ILogger logger);

	[LoggerMessage(14, LogLevel.Information, "Discord webhook success")]
	public static partial void DiscordWebhookSuccess(this ILogger logger);

	[LoggerMessage(15, LogLevel.Information, "Database backup created `{TempFileName}` at {Now} in {Elapsed}ms")]
	public static partial void DatabaseBackup(this ILogger logger, string tempFileName, long now, long elapsed);

	[LoggerMessage(16, LogLevel.Information, "Payload delivery completed in {Elapsed}ms")]
	public static partial void PayloadDeliveryComplete(this ILogger logger, long elapsed);

	[LoggerMessage(17, LogLevel.Information, "Guild created")]
	public static partial void GuildCreated(this ILogger logger);

	[LoggerMessage(18, LogLevel.Information, "Loot (un)locked - {Enabled}")]
	public static partial void LootLocked(this ILogger logger, bool enabled);

	[LoggerMessage(19, LogLevel.Information, "Alt linked - {AltName}")]
	public static partial void AltLinked(this ILogger logger, string altName);

	[LoggerMessage(20, LogLevel.Information, "Alt unlinked - {AltName}")]
	public static partial void AltUnlinked(this ILogger logger, string altName);

	[LoggerMessage(21, LogLevel.Information, "Guild leadership transferred from `{OldGuildLeaderName}` to `{NewGuildLeaderName}`")]
	public static partial void GuildLeadershipTransferred(this ILogger logger, string oldGuildLeaderName, string newGuildLeaderName);

	[LoggerMessage(22, LogLevel.Error, "Broken connection detected - {ConnectionId}")]
	public static partial void BrokenConnection(this ILogger logger, Exception ex, string connectionId);

	[LoggerMessage(23, LogLevel.Information, "Loot quantity updated")]
	public static partial void LootQuantityUpdated(this ILogger logger);

	[LoggerMessage(24, LogLevel.Error, "Discord Webhook Failure")]
	public static partial void DiscordWebhookFailure(this ILogger logger, Exception ex);

	[LoggerMessage(25, LogLevel.Information, "Database vacuum success in {Elapsed}ms")]
	public static partial void DatabaseVacuumSuccess(this ILogger logger, long elapsed);

	[LoggerMessage(26, LogLevel.Information, "Application started")]
	public static partial void ApplicationStarted(this ILogger logger);

	[LoggerMessage(27, LogLevel.Information, "Application stopping - {CancellationRequested}")]
	public static partial void ApplicationStopping(this ILogger logger, bool cancellationRequested);

	[LoggerMessage(28, LogLevel.Information, "Application stopped - {CancellationRequested}")]
	public static partial void ApplicationStopped(this ILogger logger, bool cancellationRequested);

	[LoggerMessage(29, LogLevel.Information, "Required player key missing")]
	public static partial void RequiredPlayerKeyMissing(this ILogger logger);

	[LoggerMessage(30, LogLevel.Information, "Removed player trying to log in")]
	public static partial void RemovedPlayerLogin(this ILogger logger);
}