using System.Collections.Frozen;

public record LootDto
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

	public required int ItemId { get; init; }
	public required byte RaidQuantity { get; init; }
	public required byte RotQuantity { get; init; }

	#region Item Stats
	public required long Sync { get; init; }
	public required byte[] Hash { get; init; } = [];
	public required Expansion Expansion { get; init; }
	public required ItemClass Classes { get; init; }
	public required long Prestige { get; init; }
	public required Slots Slots { get; init; }
	public required byte Regen { get; set; }
	public required byte ManaRegen { get; set; }
	public required byte EnduranceRegen { get; set; }
	public required int HealAmt { get; set; }
	public required int SpellDmg { get; set; }
	public required int Clairvoyance { get; set; }
	public required int Attack { get; set; }
	public required byte Itemtype { get; init; }
	public required byte Augslot1type { get; init; }
	public required byte Augslot3type { get; init; }
	public required byte Augslot4type { get; init; }
	public required int Stacksize { get; init; }
	public required int HP { get; init; }
	public required int Mana { get; init; }
	public required int Endurance { get; init; }
	public required int AC { get; init; }
	public required int Icon { get; init; }
	public required int Damage { get; init; }
	public required byte Delay { get; init; }
	public required byte ReqLevel { get; init; }
	public required byte RecLevel { get; init; }
	public required int HSTR { get; init; }
	public required int HINT { get; init; }
	public required int HWIS { get; init; }
	public required int HAGI { get; init; }
	public required int HDEX { get; init; }
	public required int HSTA { get; init; }
	public required int HCHA { get; init; }
	public required byte MinLuck { get; init; }
	public required byte MaxLuck { get; init; }
	public required bool Lore { get; init; }
	public required byte ProcLevel { get; init; }
	public required byte FocusLevel { get; init; }
	public required int ProcEffect { get; init; }
	public required int FocusEffect { get; init; }
	public required int ClickEffect { get; init; }
	public required int ClickLevel { get; init; }
	public required int WornEffect { get; init; }
	public required string CharmFile { get; init; }

	public string? WornName { get; init; }

	public string? ProcName { get; init; }
	public string? ProcDescription { get; init; }
	public string? ProcDescription2 {get; init; }

	public string? ClickName { get; init; }
	public string? ClickDescription { get; init; }
	public string? ClickDescription2 { get; init; }

	public string? FocusName { get; init; }
	public string? FocusDescription { get; init; }
	public string? FocusDescription2 { get; init; }



	#endregion

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
}
