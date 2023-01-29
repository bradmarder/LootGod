namespace LootGod;

public record LootDto
{
	public required int Id { get; init; }
	public required byte Quantity { get; init; }
	public required string Name { get; init; }

	public virtual bool IsSpell =>
		Loot.Nuggets.Contains(Name)
		|| (Loot.SpellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& Loot.SpellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
}
