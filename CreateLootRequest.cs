using System.ComponentModel.DataAnnotations;

namespace LootGod;

public class CreateLootRequest
{
	[StringLength(24)]
	public string MainName { get; set; } = null!;

	[StringLength(24)]
	public string CharacterName { get; set; } = null!;

	[StringLength(255)]
	public string? Spell { get; set; }

	//public bool IsMain { get; set; }
	public EQClass Class { get; set; } // Enum.IsDefined
	public int LootId { get; set; }
	public byte Quantity { get; set; }
}
