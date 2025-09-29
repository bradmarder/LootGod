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
	public Item() { }
	public Item(ItemParseOutput data, long sync)
	{
		Sync = sync;
		Id = data.Id;
		Name = data.Name;
		Hash = data.Hash;
		Expansion = data.Expansion;
		Classes = data.Classes;
		Prestige = data.Prestige;
		Slots = data.Slots;
		Regen = data.Regen;
		ManaRegen =  data.ManaRegen;
		EnduranceRegen = data.EnduranceRegen;
		HealAmt = data.HealAmt;
		SpellDmg = data.SpellDmg;
		Clairvoyance = data.Clairvoyance;
		Attack = data.Attack;
		Itemtype = data.Itemtype;
		Augslot1type = data.Augslot1type;
		Augslot3type = data.Augslot3type;
		Augslot4type = data.Augslot4type;
		Stacksize = data.Stacksize;
		HP = data.HP;
		Mana = data.Mana;
		Endurance = data.Endurance;
		AC = data.AC;
		Icon = data.Icon;
		Damage = data.Damage;
		Delay = data.Delay;
		ReqLevel = data.ReqLevel;
		RecLevel = data.RecLevel;
		HSTR = data.HSTR;
		HINT = data.HINT;
		HWIS = data.HWIS;
		HAGI = data.HAGI;
		HDEX = data.HDEX;
		HSTA = data.HSTA;
		HCHA = data.HCHA;
		MinLuck = data.MinLuck;
		MaxLuck = data.MaxLuck;
		Lore = data.Lore;
		ProcLevel = data.ProcLevel;
		FocusLevel = data.FocusLevel;
		ProcEffect = data.ProcEffect;
		FocusEffect = data.FocusEffect;
		ClickEffect = data.ClickEffect;
		ClickLevel = data.ClickLevel;
		WornEffect = data.WornEffect;
	}

	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	/// <summary>
	/// timestamp of last sync
	/// </summary>
	public long Sync { get; set; }

	[Required]
	[StringLength(255)]
	public string Name { get; set; } = null!;

	/// <summary>
	/// hash of the item data, used to detect changes
	/// </summary>
	public byte[] Hash { get; set; } = [];

	public Expansion Expansion { get; set; }
	public ItemClass Classes { get; set; }
	public long Prestige { get; set; }
	public Slots Slots { get; set; }
	public byte Regen { get; set; }
	public byte ManaRegen { get; set; }
	public byte EnduranceRegen { get; set; }
	public int HealAmt { get; set; }
	public int SpellDmg { get; set; }
	public int Clairvoyance { get; set; }
	public int Attack { get; set; }
	public byte Itemtype { get; set; }
	public byte Augslot1type { get; set; }
	public byte Augslot3type { get; set; }
	public byte Augslot4type { get; set; }
	public int Stacksize { get; set; }
	public int HP { get; set; }
	public int Mana { get; set; }
	public int Endurance { get; set; }
	public int AC { get; set; }
	public int Icon { get; set; }
	public int Damage { get; set; }
	public byte Delay { get; set; }
	public byte ReqLevel { get; set; }
	public byte RecLevel { get; set; }
	public int HSTR { get; set; }
	public int HINT { get; set; }
	public int HWIS { get; set; }
	public int HAGI { get; set; }
	public int HDEX { get; set; }
	public int HSTA { get; set; }
	public int HCHA { get; set; }
	public byte MinLuck { get; set; }
	public byte MaxLuck { get; set; }
	public bool Lore { get; set; }
	public byte ProcLevel { get; set; }
	public byte FocusLevel { get; set; }
	public int ProcEffect { get; set; }
	public int FocusEffect { get; set; }
	public int ClickEffect { get; set; }
	public int ClickLevel { get; set; }
	public int WornEffect { get; set; }

	[InverseProperty(nameof(LootRequest.Item))]
	public virtual List<LootRequest> LootRequests { get; } = [];

	[InverseProperty(nameof(Loot.Item))]
	public virtual List<Loot> Loots { get; } = [];


}
