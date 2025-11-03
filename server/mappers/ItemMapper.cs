using Riok.Mapperly.Abstractions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ItemMapper
{
	[MapperIgnoreTarget(nameof(Item.WornSpell))]
	[MapperIgnoreTarget(nameof(Item.ClickSpell))]
	[MapperIgnoreTarget(nameof(Item.ProcSpell))]
	[MapperIgnoreTarget(nameof(Item.FocusSpell))]
	public static partial Item ItemOutputMap(ItemParseOutput data, long sync);
}
