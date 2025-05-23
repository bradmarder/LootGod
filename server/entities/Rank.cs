﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Index(nameof(GuildId), nameof(Name), IsUnique = true)]
public class Rank
{
	public const string Leader = "Leader";

	private Rank() { }
	public Rank(string name, Guild guild)
	{
		Name = name;
		Guild = guild;
	}
	public Rank(string name, int guildId)
	{
		Name = name;
		GuildId = guildId;
	}

	[Key]
	public int Id { get; set; }

	public int GuildId { get; set; }

	public long CreatedDate { get; set; }

	[Required]
	[StringLength(byte.MaxValue)]
	public string Name { get; set; } = null!;

	[ForeignKey(nameof(GuildId))]
	public virtual Guild Guild { get; set; } = null!;

	[InverseProperty(nameof(Player.Rank))]
	public virtual List<Player> Players { get; } = [];
}
