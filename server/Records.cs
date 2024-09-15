public record CreateLoot(byte Quantity, int ItemId, bool RaidNight);
public record CreateGuild(string LeaderName, string GuildName, Server Server);
public record Hooks(string Raid, string Rot);
public record LootOutput(string Loot, string Name, int Quantity);
public record LootLock(bool Enable);
public record FinishLoots(bool RaidNight);
public record GrantLootRequest(int Id, bool Grant);
public record UpdateGuildDiscord(bool RaidNight, string Webhook);
public record MessageOfTheDay(string Message);
public record TransferGuildName(string Name);
public record ToggleHiddenAdminPlayer(string Name);

/// <summary>
/// null GuildId implies the payload is sent to every client (items)
/// </summary>
public record Payload(int? GuildId, string Event, string JsonData);

public class SelfDestruct(string path) : IDisposable
{
	public void Dispose() => File.Delete(path);
}

public class DataSink
{
	private int EventId = 1;

	public required int GuildId { get; init; }
	public required HttpResponse Response { get; init; }
	public required CancellationToken Token { get; init; }

	public int IncrementEventId() => EventId++;
}

public class ImportException(string message) : Exception(message) { }
