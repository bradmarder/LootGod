namespace LootGod;

public record RaidAttendanceDto
{
	public string Name { get; init; } = null!;
	public bool Hidden { get; init; }
	public bool Admin { get; init; }
	public string Rank { get; init; } = null!;
	public byte _30 { get; init; }
	public byte _90 { get; init; }
	public byte _180 { get; init; }
}
