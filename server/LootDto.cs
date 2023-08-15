namespace LootGod;

public record LootDto
{
	private static readonly HashSet<string> _spellPrefixes = new(StringComparer.OrdinalIgnoreCase)
	{
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	};
	private static readonly HashSet<string> _spellSuffixes = new(StringComparer.OrdinalIgnoreCase)
	{
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
		"Shar Vahl",
	};
	private static readonly HashSet<string> _nuggets = new(StringComparer.OrdinalIgnoreCase)
	{
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	};

	public required int Id { get; init; }
	public required byte RaidQuantity { get; init; }
	public required byte RotQuantity { get; init; }
	public required string Name { get; init; }

	public virtual bool IsSpell =>
		_nuggets.Contains(Name)
		|| (_spellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& _spellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
}
