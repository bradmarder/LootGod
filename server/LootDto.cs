using System.Collections.Frozen;

public record LootDto
{
	private static readonly FrozenDictionary<string, int> _spellNameLevelMap = new Dictionary<string, int>
	{
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
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	];
	private static readonly FrozenSet<string> _spellSuffixes =
	[
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
