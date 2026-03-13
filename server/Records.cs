public record CreateLoot(byte Quantity, int ItemId, bool RaidNight);
public record CreateGuild(string LeaderName, string GuildName, Server Server);
public record DiscordWebhooks(string Raid, string Rot);
public record LootOutput(string Loot, string Name, int Quantity);
public record LootLock(bool Enable);
public record FinishLoots(bool RaidNight);
public record GrantLootRequest(int Id, bool Grant);
public record UpdateGuildDiscord(bool RaidNight, string Webhook);
public record MessageOfTheDay(string Message);
public record TransferGuildName(string Name);
public record MakeGuest(string Name);
public record ToggleHiddenAdminPlayer(int Id);
public record ChangePlayerName(int Id, string Name);
public record ItemSearch
{
	public int Id { get; init; }
	public string Name { get; init; } = null!;
}
public record DiscordWebhookContent(string Content);
public record SpellDto
{
	public int Id { get; init; }
	public string ClassLevel { get; init; } = null!;
	public string Name { get; init; } = null!;
}

/// <summary>
/// null GuildId implies the payload is sent to every client (items)
/// </summary>
public record Payload(int? GuildId, string Event, string JsonData);

public record DataSink(int GuildId, HttpContext Context)
{
	public int EventId { get; set; } = 1;
}

public class ImportException(string message) : Exception(message) { }
public class MissingPlayerKeyException : Exception { }
public record PlayerDto(EQClass Class);

public class LogState : Dictionary<string, object?>
{
	public LogState() { }
	public LogState(string key, object? value)
	{
		this[key] = value;
	}

	public override string ToString()
	{
		var values = this.Select(x => x.Key + ":" + x.Value?.ToString());

		return string.Join(", ", values);
	}
}