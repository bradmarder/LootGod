namespace LootGod;

public record LootRequestDto
{
	public required int Id { get; init; }
	public required int PlayerId { get; init; }
	public required DateTime CreatedDate { get; init; }
	public required string MainName { get; init; }
	public required string? AltName { get; init; }
	public required string? Spell { get; init; }
	public required EQClass Class { get; init; }
	public required int LootId { get; init; }
	public required string LootName { get; init; }
	public required int Quantity { get; init; }
	public required bool RaidNight { get; init; }
	public required bool IsAlt { get; init; }
	public required bool Granted { get; init; }
	public required string CurrentItem { get; init; }
}
