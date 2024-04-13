using System.Collections.Frozen;

namespace LootGod;

public record LootDto
{
	private static readonly FrozenSet<string> _spellPrefixes = FrozenSet.ToFrozenSet(
	[
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	]);

	private static readonly FrozenSet<string> _spellSuffixes = FrozenSet.ToFrozenSet(
	[
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
		"Shar Vahl",
		"Emblem of the Forge",
	]);

	private static readonly FrozenSet<string> _nuggets = FrozenSet.ToFrozenSet(
	[
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	]);

	public required int ItemId { get; init; }
	public required byte RaidQuantity { get; init; }
	public required byte RotQuantity { get; init; }
	public required string Name { get; init; }

	public virtual bool IsSpell =>
		_nuggets.Contains(Name)
		|| (_spellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& _spellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
}
