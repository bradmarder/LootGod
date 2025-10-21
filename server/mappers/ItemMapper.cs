using Riok.Mapperly.Abstractions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ItemMapper
{
	public static partial IQueryable<ItemDto> ProjectToDto(this IQueryable<Item> q);

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
