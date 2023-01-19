using System.ComponentModel.DataAnnotations;

namespace LootGod;

public class CreateLootRequest
{
	public bool RaidNight { get; set; }

	[StringLength(24, MinimumLength = 4)]
	public string? AltName { get; set; }

	[StringLength(255)]
	public string? Spell { get; set; }

	public EQClass? Class { get; set; } // Enum.IsDefined
	public int LootId { get; set; }
	public byte Quantity { get; set; }

	[StringLength(255)]
	public string CurrentItem { get; set; } = null!;
}
