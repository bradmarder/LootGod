using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public enum Expansion : long
{
	CoV = 27,
	ToL = 28,
	NoS = 29,
	LS = 30,
	ToB = 31,
	SoR = 32,
	Unknown = 255,
}

/// <summary>
/// there is a value that is ushort.MaxValue + 1, so must use int
/// </summary>
[Flags]
public enum ItemClass
{
	None = 0,
	War = 1,
	Cleric = 1 << 1,
	Paladin = 1 << 2,
	Ranger = 1 << 3,
	Shadowknight = 1 << 4,
	Druid = 1 << 5,
	Monk = 1 << 6,
	Bard = 1 << 7,
	Rogue = 1 << 8,
	Shaman = 1 << 9,
	Necromancer = 1 << 10,
	Wizard = 1 << 11,
	Magician = 1 << 12,
	Enchanter = 1 << 13,
	Beastlord = 1 << 14,
	Berserker = 1 << 15,
	
	All = ushort.MaxValue
}

[Flags]
public enum Slots
{
	Charm = 1,
	Head = 1 << 2,
	Face = 1 << 3,
	Ear = 18,
	Neck = 1 << 5,
	Shoulders = 1 << 6,
	Arms = 1 << 7,
	Back = 1 << 8,
	Wrist = 1536, // 9 + 10
	Fingers = 98304,
	Range = 1 << 11,
	Hands = 1 << 12,
	Primary = 1 << 13,
	Secondary = 16384,
	Waist = 1048576,
	Chest = 1 << 17,
	Legs = 1 << 18,
	Feet = 1 << 19,
}

[Index(nameof(Expansion))]
public class Item
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; init; }

	/// <summary>
	/// timestamp of last sync
	/// </summary>
	public long Sync { get; init; }

	[Required]
	[StringLength(255)]
	public string Name { get; init; } = null!;

	/// <summary>
	/// hash of the item data, used to detect changes
	/// </summary>
	public byte[] Hash { get; init; } = [];

	public Expansion Expansion { get; init; }
	public ItemClass Classes { get; init; }
	public long Prestige { get; init; }
	public Slots Slots { get; init; }
	public byte CR { get; init; }
	public byte DR { get; init; }
	public byte PR { get; init; }
	public byte MR { get; init; }
	public byte FR { get; init; }
	public byte SVCorruption { get; init; }
	public byte Regen { get; init; }
	public byte ManaRegen { get; init; }
	public byte EnduranceRegen { get; init; }
	public int HealAmt { get; init; }
	public int SpellDmg { get; init; }
	public int Clairvoyance { get; init; }
	public int Attack { get; init; }
	public byte Itemtype { get; init; }
	public byte Augslot1type { get; init; }
	public byte Augslot3type { get; init; }
	public byte Augslot4type { get; init; }
	public int Stacksize { get; init; }
	public int HP { get; init; }
	public int Mana { get; init; }
	public int Endurance { get; init; }
	public int AC { get; init; }
	public int Icon { get; init; }
	public int Damage { get; init; }
	public byte Delay { get; init; }
	public byte ReqLevel { get; init; }
	public byte RecLevel { get; init; }
	public int HSTR { get; init; }
	public int HINT { get; init; }
	public int HWIS { get; init; }
	public int HAGI { get; init; }
	public int HDEX { get; init; }
	public int HSTA { get; init; }
	public int HCHA { get; init; }
	public byte MinLuck { get; init; }
	public byte MaxLuck { get; init; }
	public bool Lore { get; init; }
	public byte ProcLevel { get; init; }
	public byte FocusLevel { get; init; }
	public int? ProcEffect { get; init; }
	public int? FocusEffect { get; init; }
	public int? ClickEffect { get; init; }
	public int ClickLevel { get; init; }
	public int? WornEffect { get; init; }

	/// <summary>
	/// Special focus for Enhanced Minion
	/// </summary>
	public int? EMFocusEffect { get; init; }

	public string CharmFile { get; init; } = "";

	[InverseProperty(nameof(LootRequest.Item))]
	public virtual List<LootRequest> LootRequests { get; } = [];

	[InverseProperty(nameof(Loot.Item))]
	public virtual List<Loot> Loots { get; } = [];

	[ForeignKey(nameof(WornEffect))]
	public virtual Spell? WornSpell { get; init; }

	[ForeignKey(nameof(ClickEffect))]
	public virtual Spell? ClickSpell { get; init; }

	[ForeignKey(nameof(ProcEffect))]
	public virtual Spell? ProcSpell { get; init; }

	[ForeignKey(nameof(FocusEffect))]
	public virtual Spell? FocusSpell { get; init; }

	[ForeignKey(nameof(EMFocusEffect))]
	public virtual Spell? EMFocusSpell { get; init; }
}
