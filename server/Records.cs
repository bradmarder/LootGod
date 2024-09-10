public record CreateLoot(byte Quantity, int ItemId, bool RaidNight);
public record CreateGuild(string LeaderName, string GuildName, Server Server);
public record Hooks(string Raid, string Rot);
public record LootOutput(string Loot, string Name, int Quantity);
public record DataSink(int GuildId, HttpResponse Response, CancellationToken Token)
{
	public int EventId { get; set; } = 1;
}

/// <summary>
/// null GuildId implies the payload is sent to every client (items)
/// </summary>
public record Payload(int? GuildId, string Event, string JsonData);

public class SelfDestruct(string path) : IDisposable
{
	public void Dispose() => File.Delete(path);
}
