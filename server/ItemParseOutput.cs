using System.Collections.Frozen;
using System.Security.Cryptography;

public record ItemParseOutput
{
	private static readonly FrozenSet<string> _t2Suffixes = [
		" of Rebellion",
		" of Eternal Reverie",
	];
	private static readonly FrozenSet<string> _t2Prefixes = [
		"Velium Endowed ",
		"Faded Hoarfrost ",
		"Faded Spectral Luminosity ",
		"Faded Waning Gibbous ",
		"Obscured ",
		"Luclinite Coagulated ",
		"Spectral Luclinite ",
		"Apparitional ",
		"Valiant ",
	];
	private static readonly FrozenSet<string> _chaseLootPrefixes = [
		"Blood-Soaked ",
	];
	private static readonly FrozenSet<string> _chaseLootSuffixes = [
		" of Memoryforged Desolation",
	];
	private static readonly FrozenSet<string> _classKeywords = [
		"Legionnaire",
		"Illuminator",
		"Exarch",
		"Natureward",
		"Soulrender",
		"Lifewalker",
		"Soulforge",
		"Loremaster",
		"Shadowscale",
		"Spiritwalker",
		"Soulslayer",
		"Frostfire",
		"Flameweaver",
		"Mindlock",
		"Dragonbrood",
		"Warmonger",
	];

	private readonly string _line;
	private readonly string[] _data;

	public ItemParseOutput(string line)
	{
		_line = line;
		_data = line.Split('|');
	}

	public byte[] Hash => MD5.HashData(System.Text.Encoding.UTF8.GetBytes(_line));
	public string Name => _data[1];
	public int Id => int.Parse(_data[5]);

	// prestige can be 4278190080, and it looks like the xpac number but it's not...
	public long Prestige => long.Parse(_data[64]);

	public ItemClass Classes => (ItemClass)long.Parse(_data[36]);

	private static readonly DateTime Oct2024 = DateTime.Parse("2024-10-01");
	private static readonly DateTime Oct2023 = DateTime.Parse("2023-10-01");
	private static readonly DateTime Oct2022 = DateTime.Parse("2022-10-01");
	private static readonly DateTime Oct2021 = DateTime.Parse("2021-10-01");
	private static readonly DateTime Oct2020 = DateTime.Parse("2020-10-01");
	public Expansion Expansion =>
		Created > Oct2024 ? Expansion.ToB
		: Created > Oct2023 ? Expansion.LS
		: Created > Oct2022 ? Expansion.NoS
		: Created > Oct2021 ? Expansion.ToL
		: Created > Oct2020 ? Expansion.CoV
		: Expansion.Unknown;

	public Slots Slots => (Slots)int.Parse(_data[11]);
	public byte Regen => byte.Parse(_data[33]);
	public byte ManaRegen => byte.Parse(_data[34]);
	public byte EnduranceRegen => byte.Parse(_data[35]);
	public int HealAmt => int.Parse(_data[227]);
	public int SpellDmg => int.Parse(_data[228]);
	public int Clairvoyance => int.Parse(_data[229]);
	public byte Itemtype => byte.Parse(_data[68]);
	public byte Augslot1type => byte.Parse(_data[90]);
	public byte Augslot2type => byte.Parse(_data[93]);
	public byte Augslot3type => byte.Parse(_data[96]);
	public byte Augslot4type => byte.Parse(_data[99]);
	public int Stacksize => int.Parse(_data[133]);
	public int HP => int.Parse(_data[29]);
	public int Mana => int.Parse(_data[30]);
	public int Endurance => int.Parse(_data[31]);
	public int AC => int.Parse(_data[32]);
	public int Icon => int.Parse(_data[13]);
	public int Damage => int.Parse(_data[62]);
	public byte Delay => byte.Parse(_data[58]);
	public byte ReqLevel => byte.Parse(_data[49]);
	public byte RecLevel => byte.Parse(_data[50]);
	public int Attack => int.Parse(_data[125]);
	public int HSTR => int.Parse(_data[219]);
	public int HINT => int.Parse(_data[220]);
	public int HWIS => int.Parse(_data[221]);
	public int HAGI => int.Parse(_data[222]);
	public int HDEX => int.Parse(_data[223]);
	public int HSTA => int.Parse(_data[224]);
	public int HCHA => int.Parse(_data[225]);
	public byte MinLuck => byte.Parse(_data[299]);
	public byte MaxLuck => byte.Parse(_data[300]);
	public bool Lore => _data[10] is "-1";
	public byte ProcLevel => byte.Parse(_data[152]);
	public byte FocusLevel => byte.Parse(_data[174]);
	public int ProcEffect => int.Parse(_data[149]);
	public int FocusEffect => int.Parse(_data[171]);
	public int ClickEffect => int.Parse(_data[138]);
	public int ClickLevel => byte.Parse(_data[141]);
	public int WornEffect => int.Parse(_data[160]);
	public int UNKNOWN77 => int.Parse(_data[279]);
	public DateTime Created => DateTime.Parse(_data[310]);

	public bool IsRaid => IsRaidGear || IsRaidContainer || IsRaidSpell;

	public bool HasRaidAugSlot =>
		Augslot1type is 8
		|| Augslot2type is 8
		|| Augslot3type is 8
		|| Augslot4type is 8;

	public bool IsRaidGear => HasRaidAugSlot
		&& Augslot4type is not 18					// ignore evolve
		&& !_chaseLootPrefixes.Any(Name.StartsWith)
		&& !_chaseLootSuffixes.Any(Name.EndsWith)
		&& !_t2Prefixes.Any(Name.StartsWith)
		&& !_t2Suffixes.Any(Name.EndsWith)
		&& !_classKeywords.Any(Name.Contains);		// probably faster/simpler to ignore visible slots

	public bool IsRaidContainer =>
		Itemtype is 11 or 67
		&& (_t2Prefixes.Any(Name.StartsWith) || _t2Suffixes.Any(Name.EndsWith));

	public bool IsRaidSpell =>
		Itemtype is 11
		&& Classes is ItemClass.All
		&& IsSpellText;

	public bool IsSpellText =>
		Name.EndsWith(" Emblem of the Forge")
		|| Name.EndsWith(" Dragontouched Rune")
		|| Name.EndsWith(" Shadowscribed Parchment")
		|| Name.EndsWith(" Symbol of Shar Vahl")
		|| (Name.StartsWith("Energized ") && Name.EndsWith(" Engram"));
}
