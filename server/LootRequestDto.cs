public record LootRequestDto
{
	public int Id { get; init; }
	public int PlayerId { get; init; }
	public long CreatedDate { get; init; }
	public string MainName { get; init; } = null!;
	public string? AltName { get; init; }
	public string? Spell { get; init; }
	public EQClass Class { get; init; }
	public int ItemId { get; init; }
	public string LootName { get; init; } = null!;
	public int Quantity { get; init; }
	public bool RaidNight { get; init; }
	public bool IsAlt { get; init; }
	public bool Granted { get; init; }
	public bool Persona { get; init; }
	public string CurrentItem { get; init; } = null!;
	public long? Archived { get; init; }

	/// <summary>
	/// Implies the player has already received this item in the past
	/// </summary>
	public bool Duplicate { get; init; }
}
