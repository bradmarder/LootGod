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

[Index(nameof(Expansion))]
[Index(nameof(Name), IsUnique = true)]
public class Loot
{
	private static readonly HashSet<string> _spellPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Minor",
		"Lesser",
		"Median",
		"Greater",
		"Glowing",
		"Captured",
	};

	private static readonly HashSet<string> _spellSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Rune",
		"Ethernere",
		"Shadowscribed Parchment",
	};

	private static readonly HashSet<string> _nuggets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Diamondized Restless Ore",
		"Calcified Bloodied Ore",
	};

	public Loot() { }
	public Loot(string name, Expansion expansion)
	{
		Name = name;
		Expansion = expansion;
	}

	[Key]
	public int Id { get; set; }

	public Expansion Expansion { get; set; }

	public byte Quantity { get; set; }

	[Required]
	[MaxLength(255)]
	public string Name { get; set; } = null!;

	[InverseProperty(nameof(LootRequest.Loot))]
	public virtual ICollection<LootRequest> LootRequests { get; } = null!;

	public virtual bool IsSpell =>
		_nuggets.Contains(Name)
		|| (_spellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
		&& _spellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
}
