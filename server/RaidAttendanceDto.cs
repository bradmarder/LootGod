public record RaidAttendanceDto
{
	public required int Id { get; init; }
	public required string Name { get; init; }
	public required bool Hidden { get; init; }
	public required bool Admin { get; init; }
	public required string Rank { get; init; }
	public required byte _30 { get; init; }
	public required byte _90 { get; init; }
	public required byte _180 { get; init; }
	public required DateOnly? LastOnDate { get; init; }
	public required string? Notes { get; init; }
	public required string? Zone { get; init; }
	public required bool Guest { get; init; }
	public required EQClass? Class { get; init; }
	public required byte? Level { get; init; }
	public required HashSet<string> Alts { get; init; }
	public required int T1GrantedLootCount { get; init; }
	public required int T2GrantedLootCount { get; init; }
}
