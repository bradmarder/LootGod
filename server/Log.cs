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

	[LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Discord Webhook Failure")]
	public static partial void DiscordWebhookFailure(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "{Method} - {Path}")]
	public static partial void MiddlewareLog(this ILogger logger, string method, string path);

	[LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Payload delivery completed")]
	public static partial void PayloadDeliveryComplete(this ILogger logger);

	[LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Broken connection detected - {ConnectionId}")]
	public static partial void BrokenConnection(this ILogger logger, Exception ex, string connectionId);

	[LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Discord read content failed")]
	public static partial void DiscordReadContentError(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "Activity logging error")]
	public static partial void ActivityLoggingError(this ILogger logger, Exception ex);

	[LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "UseExceptionHandler - {RequestPath}")]
	public static partial void UseExceptionHandler(this ILogger logger, Exception ex, string requestPath);
}
