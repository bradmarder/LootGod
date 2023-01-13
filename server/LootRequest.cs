using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(CreatedDate))]
[Index(nameof(Archived), nameof(Granted))]
public class LootRequest
{
	public LootRequest() { }
	public LootRequest(CreateLootRequest dto, string? ip, int playerId)
	{
		IP = ip;
		PlayerId = playerId;
		AltName = dto.AltName?.Trim();
		Spell = dto.Spell?.Trim();
		Class = dto.Class;
		LootId = dto.LootId;
		Quantity = dto.Quantity;
		CurrentItem = dto.CurrentItem;
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	public string? IP { get; set; }

	//[Required]
	[MaxLength(24)]
	public string? AltName { get; set; } = null!;

	/// <summary>
	/// Required only if loot type is a spell or nugget
	/// </summary>
	[MaxLength(255)]
	public string? Spell { get; set; }

	public EQClass? Class { get; set; }
	public int LootId { get; set; }

	/// <summary>
	/// True for "raid night" loots, false for "Rot Loot"
	/// </summary>
	public bool RaidNight { get; set; }

	public bool Granted { get; set; }
	public bool Archived { get; set; }
	public int PlayerId { get; set; }

	[Range(1, 255)]
	public byte Quantity { get; set; }

	[MaxLength(255)]
	public string CurrentItem { get; set; } = null!;

	public virtual bool IsAlt => AltName is not null;

	[ForeignKey(nameof(LootId))]
	public virtual Loot Loot { get; set; } = null!;

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}
