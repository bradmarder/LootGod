namespace LootGod;

public record RaidAttendanceDto
{
	public required string Name { get; init; }
	public required bool Hidden { get; init; }
	public required bool Admin { get; init; }
	public required string Rank { get; init; }
	public required byte _30 { get; init; }
	public required byte _90 { get; init; }
	public required byte _180 { get; init; }
}
