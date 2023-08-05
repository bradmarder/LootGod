﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

public enum Expansion : byte
{
	CoV = 0,
	ToL = 1,
	NoS = 3,
}

[Index(nameof(Expansion), nameof(GuildId))]
[Index(nameof(Name), nameof(GuildId), IsUnique = true)]
public class Loot
{
	public static readonly HashSet<string> SpellPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	};

	public static readonly HashSet<string> SpellSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
		"Shar Vahl",
	};

	public static readonly HashSet<string> Nuggets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	};

	private Loot() { }
	public Loot(string name, Expansion expansion, int guildId)
	{
		Name = name;
		Expansion = expansion;
		GuildId = guildId;
	}

	public Loot(string name, Expansion expansion, Guild guild)
	{
		Name = name;
		Expansion = expansion;
		Guild = guild;
	}

	[Key]
	public int Id { get; set; }

	public Expansion Expansion { get; set; }

	public byte RaidQuantity { get; set; }

	public byte RotQuantity { get; set; }

	public int GuildId { get; set; }

	[Required]
	[MaxLength(255)]
	public string Name { get; set; } = null!;

	[ForeignKey(nameof(GuildId))]
	public virtual Guild? Guild { get; set; } = null!;

	[InverseProperty(nameof(LootRequest.Loot))]
	public virtual List<LootRequest> LootRequests { get; } = new();
}
