using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

public enum Expansion : byte
{
	CoV = 0,
	ToL = 1,
	NoS = 3,
	LS = 4,
}

// TODO: rebuild table with correct indexes
[Index(nameof(Expansion))]
[Index(nameof(Name), IsUnique = true)]
public class Item
{
	private Item() { }
	public Item(string name, Expansion expansion, Guild guild)
	{
		Name = name;
		Expansion = expansion;
		Guild = guild;
	}

	[Key]
	public int Id { get; private set; }

	public Expansion Expansion { get; set; }

	[Obsolete]
	public int GuildId { get; set; }

	[Required]
	[StringLength(255)]
	public string Name { get; set; } = null!;

	[Obsolete]
	[ForeignKey(nameof(GuildId))]
	public virtual Guild? Guild { get; set; } = null!;

	[InverseProperty(nameof(LootRequest.Item))]
	public virtual List<LootRequest> LootRequests { get; } = new();

	[InverseProperty(nameof(Loot.Item))]
	public virtual List<Loot> Loots { get; } = new();
}
