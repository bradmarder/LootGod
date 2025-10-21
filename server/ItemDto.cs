using System.Collections.Frozen;

public record ItemDto
{
	private static readonly FrozenDictionary<string, int> _spellNameLevelMap = new Dictionary<string, int>
	{
		["Energized Glowing Engram"] = 125,
		["Energized Greater Engram"] = 124,
		["Energized Median Engram"] = 123,
		["Energized Lesser Engram"] = 122,
		["Energized Minor Engram"] = 121,

		["Glowing Emblem of the Forge"] = 125,
		["Greater Emblem of the Forge"] = 124,
		["Median Emblem of the Forge"] = 123,
		["Lesser Emblem of the Forge"] = 122,
		["Minor Emblem of the Forge"] = 121,

		["Glowing Symbol of Shar Vahl"] = 120,
		["Greater Symbol of Shar Vahl"] = 119,
		["Median Symbol of Shar Vahl"] = 118,
		["Lesser Symbol of Shar Vahl"] = 117,
		["Minor Symbol of Shar Vahl"] = 116,
	}.ToFrozenDictionary();
	private static readonly FrozenSet<string> _spellPrefixes =
	[
		"Energized",
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	];
	private static readonly FrozenSet<string> _spellSuffixes =
	[
		"Engram",
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
		"Shar Vahl",
		"Emblem of the Forge",
	];
	private static readonly FrozenSet<string> _nuggets =
	[
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	];

	private readonly string _name = "";
	public required string Name
	{
		get => _spellNameLevelMap.TryGetValue(_name, out var level)
			? level + " | " + _name
			: _name;
		init => _name = value;
	}

	public virtual bool IsSpell =>
		_nuggets.Contains(_name)
		|| (_spellPrefixes.Any(x => _name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& _spellSuffixes.Any(x => _name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

	public int Id { get; init; }
	public long Sync { get; init; }
	public byte[] Hash { get; init; } = [];
	public Expansion Expansion { get; init; }
	public ItemClass Classes { get; init; }
	public long Prestige { get; init; }
	public Slots Slots { get; init; }
	public byte Regen { get; set; }
	public byte ManaRegen { get; set; }
	public byte EnduranceRegen { get; set; }
	public int HealAmt { get; set; }
	public int SpellDmg { get; set; }
	public int Clairvoyance { get; set; }
	public int Attack { get; set; }
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
	public int ProcEffect { get; init; }
	public int FocusEffect { get; init; }
	public int ClickEffect { get; init; }
	public int ClickLevel { get; init; }
	public int WornEffect { get; init; }
	public string CharmFile { get; init; } = null!;
	public string? WornName { get; init; }
	public string? ProcName { get; init; }
	public string? ProcDescription { get; init; }
	public string? ProcDescription2 { get; init; }
	public string? ClickName { get; init; }
	public string? ClickDescription { get; init; }
	public string? ClickDescription2 { get; init; }
	public string? FocusName { get; init; }
	public string? FocusDescription { get; init; }
	public string? FocusDescription2 { get; init; }
}
