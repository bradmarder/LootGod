using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Index(nameof(CreatedDate))]
[Index(nameof(PlayerId), nameof(Archived), nameof(Granted))]
public class LootRequest
{
	private LootRequest() { }
	public LootRequest(CreateLootRequest dto, string? ip, int playerId)
	{
		IP = ip;
		PlayerId = playerId;
		Spell = dto.Spell?.Trim();
		Class = dto.Class;
		ItemId = dto.ItemId;
		Quantity = dto.Quantity;
		CurrentItem = dto.CurrentItem;
		RaidNight = dto.RaidNight;

		// The UI should prevent entering AltName for RaidNight loot
		if (!dto.RaidNight)
		{
			AltName = dto.AltName?.Trim();
		}
	}

	[Key]
	public int Id { get; set; }

	public long CreatedDate { get; set; }

	public string? IP { get; set; }

	[StringLength(24, MinimumLength = 4)]
	public string? AltName { get; set; } = null!;

	/// <summary>
	/// Required only if loot type is a spell or nugget
	/// </summary>
	[StringLength(255)]
	public string? Spell { get; set; }

	/// <summary>
	/// Why have this property here and not just use Player.Class? Because the player may be requesting loot for a persona
	/// </summary>
	public EQClass? Class { get; set; }

	public int ItemId { get; set; }

	/// <summary>
	/// True for "raid night" loots, false for "Rot Loot"
	/// </summary>
	public bool RaidNight { get; set; }

	public bool Granted { get; set; }
	public bool Archived { get; set; }
	public int PlayerId { get; set; }

	[Range(1, 255)]
	public byte Quantity { get; set; }

	[StringLength(255)]
	public string CurrentItem { get; set; } = null!;

	public virtual bool IsAlt => AltName is not null;

	[ForeignKey(nameof(ItemId))]
	public virtual Item Item { get; set; } = null!;

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}
