using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Spell
{
	public Spell() { }
	public Spell(SpellParseOutput data, long sync)
	{
		Sync = sync;
		Id = data.Id;
		Name = data.Name;
		Hash = data.Hash;
		Description = data.Description;
		Description2 = data.Description2;
	}

	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; init; }

	/// <summary>
	/// timestamp of last sync
	/// </summary>
	public long Sync { get; init; }

	[Required]
	[StringLength(255)]
	public string Name { get; init; } = null!;

	/// <summary>
	/// hash of the item data, used to detect changes
	/// </summary>
	public byte[] Hash { get; init; } = [];

	public string Description { get; init; } = null!;
	public string Description2 { get; init; } = null!;
}
