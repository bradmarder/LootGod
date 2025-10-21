using Riok.Mapperly.Abstractions;

//[UseStaticMapper(typeof(ItemMapper))] // does not work because its brings the map method, not the queryable projection
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LootMapper
{
	public static partial IQueryable<LootDto> ProjectToDto(this IQueryable<Loot> q);

	public static partial LootDto LootMap(Loot loot);

	[MapperIgnoreTarget(nameof(ItemDto.IsSpell))]
	[MapperIgnoreTarget(nameof(ItemDto.WornName))]
	[MapperIgnoreTarget(nameof(ItemDto.ClickName))]
	[MapperIgnoreTarget(nameof(ItemDto.ClickDescription))]
	[MapperIgnoreTarget(nameof(ItemDto.ClickDescription2))]
	[MapperIgnoreTarget(nameof(ItemDto.ProcName))]
	[MapperIgnoreTarget(nameof(ItemDto.ProcDescription))]
	[MapperIgnoreTarget(nameof(ItemDto.ProcDescription2))]
	[MapperIgnoreTarget(nameof(ItemDto.FocusName))]
	[MapperIgnoreTarget(nameof(ItemDto.FocusDescription))]
	[MapperIgnoreTarget(nameof(ItemDto.FocusDescription2))]
	public static partial ItemDto ItemMap(Item item);
}
