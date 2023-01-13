using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(Name), nameof(GuildId), IsUnique = true)]
public class Rank
{

	public Rank() { }
	public Rank(string name, int guildId)
	{
		Name = name;
		GuildId = guildId;
	}

	[Key]
	public int Id { get; set; }

	public int GuildId { get; set; }

	public DateTime CreatedDate { get; set; }

	[Required]
	[MaxLength(byte.MaxValue)]
	public string Name { get; set; } = null!;

	[ForeignKey(nameof(GuildId))]
	public virtual Guild Guild { get; set; } = null!;

	[InverseProperty(nameof(Player.Rank))]
	public virtual ICollection<Player> Players { get; } = null!;
}
