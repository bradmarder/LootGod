using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(Name), IsUnique = true)]
public class Rank
{

	public Rank() { }
	public Rank(string name)
	{
		Name = name;
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	[Required]
	[MaxLength(byte.MaxValue)]
	public string Name { get; set; } = null!;

	[InverseProperty(nameof(Player.Rank))]
	public virtual ICollection<Player> Players { get; } = null!;
}
