using System.Collections.Frozen;

namespace LootGod;

public record LootDto
{
	private static readonly FrozenSet<string> _spellPrefixes = new[]
	{
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	}.ToFrozenSet();

	private static readonly FrozenSet<string> _spellSuffixes = new[]
	{
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
		"Shar Vahl",
		"Emblem of the Forge",
	}.ToFrozenSet();

	private static readonly FrozenSet<string> _nuggets = new[]
	{
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	}.ToFrozenSet();

	public required int Id { get; init; }
	public required byte RaidQuantity { get; init; }
	public required byte RotQuantity { get; init; }
	public required string Name { get; init; }

	public virtual bool IsSpell =>
		_nuggets.Contains(Name)
		|| (_spellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& _spellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
}
