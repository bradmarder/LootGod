using Riok.Mapperly.Abstractions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ItemMapper
{
	public static partial IQueryable<ItemDto> ProjectToDto(this IQueryable<Item> q);

	[MapperIgnoreTarget(nameof(Item.WornSpell))]
	[MapperIgnoreTarget(nameof(Item.ClickSpell))]
	[MapperIgnoreTarget(nameof(Item.ProcSpell))]
	[MapperIgnoreTarget(nameof(Item.FocusSpell))]
	public static partial Item ItemOutputMap(ItemParseOutput data, long sync);

	[MapperIgnoreTarget(nameof(ItemDto.IsSpell))]
	[MapPropertyFromSource(nameof(ItemDto.WornName), Use = nameof(MapWornName))]
	[MapPropertyFromSource(nameof(ItemDto.ClickName), Use = nameof(MapClickName))]
	[MapPropertyFromSource(nameof(ItemDto.ClickDescription), Use = nameof(MapClickDescription))]
	[MapPropertyFromSource(nameof(ItemDto.ClickDescription2), Use = nameof(MapClickDescription2))]
	[MapPropertyFromSource(nameof(ItemDto.ProcName), Use = nameof(MapProcName))]
	[MapPropertyFromSource(nameof(ItemDto.ProcDescription), Use = nameof(MapProcDescription))]
	[MapPropertyFromSource(nameof(ItemDto.ProcDescription2), Use = nameof(MapProcDescription2))]
	[MapPropertyFromSource(nameof(ItemDto.FocusName), Use = nameof(MapFocusName))]
	[MapPropertyFromSource(nameof(ItemDto.FocusDescription), Use = nameof(MapFocusDescription))]
	[MapPropertyFromSource(nameof(ItemDto.FocusDescription2), Use = nameof(MapFocusDescription2))]
	public static partial ItemDto ItemMap(Item item);

	private static string? MapWornName(Item item) => item.WornSpell!.Name;
	private static string? MapClickName(Item item) => item.ClickSpell!.Name;
	private static string? MapClickDescription(Item item) => item.ClickSpell!.Description;
	private static string? MapClickDescription2(Item item) => item.ClickSpell!.Description2;
	private static string? MapProcName(Item item) => item.ProcSpell!.Name;
	private static string? MapProcDescription(Item item) => item.ProcSpell!.Description;
	private static string? MapProcDescription2(Item item) => item.ProcSpell!.Description2;
	private static string? MapFocusName(Item item) => item.FocusSpell!.Name;
	private static string? MapFocusDescription(Item item) => item.FocusSpell!.Description;
	private static string? MapFocusDescription2(Item item) => item.FocusSpell!.Description2;
}
