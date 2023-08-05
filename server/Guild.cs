using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(Name), IsUnique = true)]
public class Guild
{
	private Guild() { }
	public Guild(string name)
	{
		Name = name;
	}

	[Key]
	public int Id { get; set; }

	[StringLength(255, MinimumLength = 3)]
	public string Name { get; set; } = null!;

	public DateTime CreatedDate { get; set; }

	public bool LootLocked { get; set; }

	// TODO: cannot delete FK column, need to re-create table
	public int? LeaderId { get; set; }

	public string? DiscordWebhookUrl { get; set; }

	[InverseProperty(nameof(Player.Guild))]
	public virtual List<Player> Players { get; } = new();

	[InverseProperty(nameof(Loot.Guild))]
	public virtual List<Loot> Loots { get; } = new();

	[InverseProperty(nameof(Rank.Guild))]
	public virtual List<Rank> Ranks { get; } = new();
}
