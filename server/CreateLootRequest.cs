using System.ComponentModel.DataAnnotations;

namespace LootGod;

public class CreateLootRequest
{
	[StringLength(24)]
	public string? AltName { get; set; }

	[StringLength(255)]
	public string? Spell { get; set; }

	public EQClass Class { get; set; } // Enum.IsDefined
	public int LootId { get; set; }
	public byte Quantity { get; set; }

	[StringLength(255)]
	public string CurrentItem { get; set; } = null!;
}
