using System.ComponentModel.DataAnnotations;

namespace LootGod;

public record CreateLootRequest
{
	public bool RaidNight { get; init; }

	[StringLength(24, MinimumLength = 4)]
	public string? AltName { get; init; }

	[StringLength(255)]
	public string? Spell { get; init; }

	public EQClass? Class { get; init; } // Enum.IsDefined
	public int LootId { get; init; }
	public byte Quantity { get; init; }

	[StringLength(255)]
	public string CurrentItem { get; init; } = null!;
}
