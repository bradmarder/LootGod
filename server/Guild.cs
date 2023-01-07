using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

public class Guild
{
	public int Id { get; set; }

	[StringLength(255)]
	public string Name { get; set; } = null!;

	[StringLength(255)]
	public string Server { get; set; } = null!;

	public DateTime CreatedDate { get; set; }

	public int? LeaderId { get; set; }

	[ForeignKey(nameof(LeaderId))]
	public virtual Player? Leader { get; set; } = null!;

	[InverseProperty(nameof(Player.Guild))]
	public virtual ICollection<Player> Players { get; } = null!;

	[InverseProperty(nameof(Loot.Guild))]
	public virtual ICollection<Loot> Loots { get; } = null!;

	[InverseProperty(nameof(LootLock.Guild))]
	public virtual ICollection<LootLock> LootLocks { get; } = null!;
}
