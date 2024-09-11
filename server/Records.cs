public record CreateLoot(byte Quantity, int ItemId, bool RaidNight);
public record CreateGuild(string LeaderName, string GuildName, Server Server);
public record Hooks(string Raid, string Rot);
public record LootOutput(string Loot, string Name, int Quantity);

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
