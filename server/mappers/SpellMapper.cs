using Riok.Mapperly.Abstractions;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class SpellMapper
{
	public static partial IQueryable<SpellDto> ProjectToDto(this IQueryable<Spell> q);
}
