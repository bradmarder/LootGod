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

[Index(nameof(Expansion), nameof(GuildId))]
[Index(nameof(Name), nameof(GuildId), IsUnique = true)]
public class Loot
{
	private Loot() { }
	public Loot(string name, Expansion expansion, Guild guild)
	{
		Name = name;
		Expansion = expansion;
		Guild = guild;
	}

	[Key]
	public int Id { get; private set; }

	public Expansion Expansion { get; set; }

	public byte RaidQuantity { get; set; }

	public byte RotQuantity { get; set; }

	public int GuildId { get; set; }

	[Required]
	[StringLength(255)]
	public string Name { get; set; } = null!;

	[ForeignKey(nameof(GuildId))]
	public virtual Guild? Guild { get; set; } = null!;

	[InverseProperty(nameof(LootRequest.Loot))]
	public virtual List<LootRequest> LootRequests { get; } = new();
}

//[Index(nameof(LootId), nameof(GuildId), IsUnique = true)]
//public class GuildLoot
//{
//	private GuildLoot() { }

//	public int LootId { get; set; }
//	public int GuildId { get; set; }
//	public byte RaidQuantity { get; set; }
//	public byte RotQuantity { get; set; }

//	[ForeignKey(nameof(GuildId))]
//	public virtual Guild Guild { get; set; } = null!;

//	[ForeignKey(nameof(LootId))]
//	public virtual Loot Loot { get; set; } = null!;
//}