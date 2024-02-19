using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(GuildId), nameof(ItemId), IsUnique = true)]
public class Loot
{
	[Key]
	public int Id { get; private set; }

	public int ItemId { get; set; }
	public int GuildId { get; set; }
	public byte RaidQuantity { get; set; }
	public byte RotQuantity { get; set; }

	[ForeignKey(nameof(GuildId))]
	public virtual Guild Guild { get; set; } = null!;

	[ForeignKey(nameof(ItemId))]
	public virtual Item Item { get; set; } = null!;
}
