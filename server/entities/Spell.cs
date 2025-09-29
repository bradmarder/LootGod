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
	public int Id { get; set; }

	/// <summary>
	/// timestamp of last sync
	/// </summary>
	public long Sync { get; set; }

	[Required]
	[StringLength(255)]
	public string Name { get; set; } = null!;

	/// <summary>
	/// hash of the item data, used to detect changes
	/// </summary>
	public byte[] Hash { get; set; } = [];

	public string Description { get; set; } = null!;
	public string Description2 { get; set; } = null!;
}
