public static partial class Log
{
	[LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Spell sync successfully completed")]
	public static partial void SpellSyncSuccess(this ILogger logger);

	[LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Item sync successfully completed")]
	public static partial void ItemSyncSuccess(this ILogger logger);

	[LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Loot requests finished")]
	public static partial void LootRequestsFinished(this ILogger logger);

	[LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "DataSink created")]
	public static partial void DataSinkCreated(this ILogger logger);

	[LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "DataSink removed after {Elapsed}s")]
	public static partial void DataSinkRemoved(this ILogger logger, int elapsed);

	[LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Loot request created")]
	public static partial void LootRequestCreated(this ILogger logger);

	[LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Loot request deleted")]
	public static partial void LootRequestDeleted(this ILogger logger);

	[LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Loot request granted - {LootRequestId} - {Granted}")]
	public static partial void LootRequestGranted(this ILogger logger, int lootRequestId, bool granted);

	[LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Discord read content failed")]
	public static partial void DiscordReadContentError(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Import dump warning - `{Message}`")]
	public static partial void ImportDumpWarning(this ILogger logger, string message);

	[LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "GlobalExceptionHandler - {RequestPath}")]
	public static partial void GlobalExceptionHandler(this ILogger logger, Exception ex, string requestPath);

	[LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Raid dump import completed")]
	public static partial void RaidDumpImportCompleted(this ILogger logger);

	[LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Guild dump import completed")]
	public static partial void GuildDumpImportCompleted(this ILogger logger);

	[LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Discord webhook success")]
	public static partial void DiscordWebhookSuccess(this ILogger logger);

	[LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Database backup created `{TempFileName}` at {Now} in {Elapsed}ms")]
	public static partial void DatabaseBackup(this ILogger logger, string tempFileName, long now, long elapsed);

	[LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "Payload delivery completed")]
	public static partial void PayloadDeliveryComplete(this ILogger logger);

	[LoggerMessage(EventId = 17, Level = LogLevel.Information, Message = "Guild created")]
	public static partial void GuildCreated(this ILogger logger);

	[LoggerMessage(EventId = 18, Level = LogLevel.Information, Message = "Loot (un)locked - {Enabled}")]
	public static partial void LootLocked(this ILogger logger, bool enabled);

	[LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "Alt linked - {AltName}")]
	public static partial void AltLinked(this ILogger logger, string altName);

	[LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "Alt unlinked - {AltName}")]
	public static partial void AltUnlinked(this ILogger logger, string altName);

	[LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Guild leadership transferred from `{OldGuildLeaderName}` to `{NewGuildLeaderName}`")]
	public static partial void GuildLeadershipTransferred(this ILogger logger, string oldGuildLeaderName, string newGuildLeaderName);

	[LoggerMessage(EventId = 22, Level = LogLevel.Error, Message = "Broken connection detected - {ConnectionId}")]
	public static partial void BrokenConnection(this ILogger logger, Exception ex, string connectionId);

	[LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Loot quantity updated")]
	public static partial void LootQuantityUpdated(this ILogger logger);

	[LoggerMessage(EventId = 24, Level = LogLevel.Error, Message = "Discord Webhook Failure")]
	public static partial void DiscordWebhookFailure(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 25, Level = LogLevel.Information, Message = "Database vacuum success in {Elapsed}ms")]
	public static partial void DatabaseVacuumSuccess(this ILogger logger, long elapsed);
}
