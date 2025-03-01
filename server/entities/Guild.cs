﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Index(nameof(Name), nameof(Server), IsUnique = true)]
public class Guild
{
	private Guild() { }
	public Guild(string name, Server server)
	{
		Name = name;
		Server = server;
	}

	[Key]
	public int Id { get; set; }

	[StringLength(255, MinimumLength = 3)]
	public string Name { get; set; } = null!;

	public Server Server { get; set; }

	public long CreatedDate { get; set; }

	public bool LootLocked { get; set; }

	[StringLength(255)]
	public string? RaidDiscordWebhookUrl { get; set; }

	[StringLength(255)]
	public string? RotDiscordWebhookUrl { get; set; }

	public string? MessageOfTheDay { get; set; }

	[InverseProperty(nameof(Player.Guild))]
	public virtual List<Player> Players { get; } = [];

	[InverseProperty(nameof(Loot.Guild))]
	public virtual List<Loot> Loots { get; } = [];

	[InverseProperty(nameof(Rank.Guild))]
	public virtual List<Rank> Ranks { get; } = [];
}

public enum Server : byte
{
	Aradune = 0,
	Mischief = 1,
	Oakwynd = 2,
	Rizlona = 3,
	Thornblade = 4,
	Vaniki = 5,
	Vox = 6,
	Yelinak = 7,
	Agnarr = 8,
	AntoniusBayle = 9,
	BertoxxulousSaryrn = 10,
	BristlebaneTheTribunal = 11,
	CazicThuleFenninRo = 12,
	DrinalMaelinStarpyre = 13,
	Mangler = 14,
	ErollisiMarrTheNameless = 15,
	FirionaVie = 16,
	LuclinStromm = 17,
	PovarQuellious = 18,
	Ragefire = 19,
	TheRathePrexus = 20,
	TunareTheSeventhHammer = 21,
	XegonyDruzzilRo = 22,
	Zek = 23,
}
