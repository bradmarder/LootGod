using Riok.Mapperly.Abstractions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LootRequestMapper
{
	public static partial IQueryable<LootRequestDto> ProjectToDto(this IQueryable<LootRequest> q);

	[MapPropertyFromSource(nameof(LootRequestDto.LootName), Use = nameof(MapLootName))]
	[MapPropertyFromSource(nameof(LootRequestDto.Class), Use = nameof(MapClass))]
	[MapPropertyFromSource(nameof(LootRequestDto.MainName), Use = nameof(MapMainName))]
	[MapPropertyFromSource(nameof(LootRequestDto.Duplicate), Use = nameof(MapDuplicate))]
	public static partial LootRequestDto LootRequestMap(LootRequest request);

	private static string MapLootName(LootRequest lr) => lr.Item.Name;
	private static EQClass MapClass(LootRequest lr) => lr.Class ?? lr.Player.Class;
	private static string MapMainName(LootRequest lr) => lr.Player.Name;
	private static bool MapDuplicate(LootRequest lr) => lr.Player.LootRequests.Any(x => x.ItemId == lr.ItemId && x.Granted && x.Archived != null);
}
