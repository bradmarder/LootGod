﻿namespace LootGod;

public class StaticData
{
	public static readonly HashSet<string> ToLLoots = new(StringComparer.OrdinalIgnoreCase)
	{
		// anni raid loots
		"Arch Convoker's Pantaloons",
		"Blood Cinder Greaves",
		"Cobalt-Reinforced Greaves",
		"Collar of Drolvarg Loyalty",
		"Drusella's Wedding Ring",
		"Eldest Spiritist's Greaves",
		"Kly Skinned Pantaloons",
		"Leggings of Wrathfulness",
		"Leggings of the Whistling Fists",
		"Necklace of the United Tribes",
		"Sathir Dagger of Sacrifice",
		"Sathir Staff of Conflict",
		"Sathir Staff of Forbidden Magic",
		"Sathir Staff of Leadership",
		"Sathir's Ring of Eternal Life",
		"Spellsung Steel Greaves",
		"Tolan's Brightwood Greaves",
		"Venril's Wedding Ring",
		"Wilderness Lord's Trousers",

		"Suit of Eclipsed Light Cloth",
		"Suit of Eclipsed Light Leather",
		"Suit of Eclipses Light Chain",
		"Suit of Eclipsed Light Plate",
		"Eclipsed Light Cloth Hands Ornament",
		"Eclipsed Light Cloth Arms Ornament",
		"Eclipsed Light Cloth Chest Ornament",
		"Eclipsed Light Cloth Feet Ornament",
		"Eclipsed Light Cloth Head Ornament",
		"Eclipsed Light Cloth Legs Ornament",
		"Eclipsed Light Cloth Wrist Ornament",
		"Eclipsed Light Cloth Robe Ornament",
		"Eclipsed Light Leather Hands Ornament",
		"Eclipsed Light Leather Arms Ornament",
		"Eclipsed Light Leather Chest Ornament",
		"Eclipsed Light Leather Feet Ornament",
		"Eclipsed Light Leather Head Ornament",
		"Eclipsed Light Leather Legs Ornament",
		"Eclipsed Light Leather Wrist Ornament",
		"Eclipsed Light Chain Hands Ornament",
		"Eclipsed Light Chain Arms Ornament",
		"Eclipsed Light Chain Chest Ornament",
		"Eclipsed Light Chain Feet Ornament",
		"Eclipsed Light Chain Head Ornament",
		"Eclipsed Light Chain Legs Ornament",
		"Eclipsed Light Chain Wrist Ornament",
		"Eclipsed Light Plate Hands Ornament",
		"Eclipsed Light Plate Arms Ornament",
		"Eclipsed Light Plate Chest Ornament",
		"Eclipsed Light Plate Feet Ornament",
		"Eclipsed Light Plate Head Ornament",
		"Eclipsed Light Plate Legs Ornament",
		"Eclipsed Light Plate Wrist Ornament",
		"Akhevan Trunk",
		"Ans Temariel Itraer",
		"Ashenfall, Labrys of the Penumbra",
		"Balance's Radiance",
		"Bite of the Vampire",
		"Bladed Wand of Night",
		"Blessed Akhevan Shadow Shears",
		"Bludgeon of the Moon's Favor",
		"Buckle of Phlebotomy",
		"Calcified Bloodied Ore",
		"Choker of Entropy",
		"Cloak of Perpetual Eventide",
		"Collar of Nyctophobia",
		"Deep Sanguine Band",
		"Diseased Netherbian Claw Staff",
		"Donaskz, Martello of Temperance",
		"Dual Partisan of the Shattered Vow",
		"Duskcaller, the Master's Herald",
		"Edge of Madness",
		"Encyclopedia Arcanum",
		"Eternal Band of Twisting Shadow",
		"Faded Waning Gibbous Arms Armor",
		"Faded Waning Gibbous Chest Armor",
		"Faded Waning Gibbous Feet Armor",
		"Faded Waning Gibbous Hands Armor",
		"Faded Waning Gibbous Head Armor",
		"Faded Waning Gibbous Legs Armor",
		"Faded Waning Gibbous Wrist Armor",
		"Goblet of the Feral",
		"Gorget of the Forgotten",
		"Guillotine of Perpetual Moonlight",
		"Holy Akhevan Mace",
		"Ilulawen, Longbow of Justice",
		"Keltakun's Last Laugh",
		"Lethia, the Master's Treasure",
		"Likeness of Whispers",
		"Locket of Luclin",
		"Loop of Infinite Twilight",
		"Lost Canto of the Maestro",
		"Lunatic Helix",
		"Mace of the Endless Twilight",
		"Mantle of the Pious",
		"Mask of Unfettered Lunacy",
		"Moonbeam, the Piercing Comet",
		"Night's Templar",
		"Rampart of Moonlit Luminosity",
		"Retribution of the Lost",
		"Sanguine Spaulders of the Heretic",
		"Sash of the Senshali",
		"Shadow Hunter's Bow",
		"Shadowpearl Stud",
		"Shred of Sanity",
		"Talisman of the Elysians",
		"The Diabo's Persuasion",
		"Threaded Cintura of Treachery",
		"Umbrablade Katar",
		"Veil of Lunar Winds",
		"Vexar Ivanieun",
		"Viscous Shroud of the Swarm",
		"Wall of Encroaching Shadow",
		"Wisdom of the Fallen Star",
		"Wito Xi Vereor",
		"Wrath, Lancia of the Blood Beast",
		"Zelnithak's Pristine Molar",
	};
	public static readonly HashSet<string> Loots = new(StringComparer.OrdinalIgnoreCase)
	{
		"Captured Essence of Ethernere",
		"Glowing Dragontouched Rune",
		"Greater Dragontouched Rune",
		"Lesser Dragontouched Rune",
		"Median Dragontouched Rune",
		"Minor Dragontouched Rune",
		"Ashrin, Last Defense",
		"Axe of Draconic Legacy",
		"Blazing Spear",
		"Bloodied Talisman",
		"Bow of Living Frost",
		"Braided Belt of Dragonshide",
		"Cloak of Mortality",
		"Commanding Blade",
		"Crusaders' Cowl",
		"Diamondized Restless Ore",
		"Dragonsfire Staff",
		"Drakescale Mask",
		"Drape of Dust,Shoulders", // TYPO
		"Dreamweaver's Axe of Banishment",
		"Drop of Klandicar's Blood",
		"Faded Hoarfrost Arms Armor",
		"Faded Hoarfrost Chest Armor",
		"Faded Hoarfrost Feet Armor",
		"Faded Hoarfrost Hands Armor",
		"Faded Hoarfrost Head Armor",
		"Faded Hoarfrost Legs Armor",
		"Faded Hoarfrost Wrist Armor",
		"First Brood Signet Ring",
		"Flame Touched Velium Slac",
		"Flametongue, Uiliak's Menace",
		"Frigid Runic Sword",
		"Frosted Scale",
		"Frozen Foil of the New Brood",
		"Frozen Gutripper",
		"Ganzito's Crystal Ring",
		"Glowing Icicle",
		"Guard of Echoes",
		"Imbued Hammer of Skyshrine",
		"Jchan's Threaded Belt of Command",
		"Klanderso's Stabber of Slaughter",
		"Linked Belt of Entrapment",
		"Mace of Crushing Energy",
		"Mace of Flowing Life",
		"Mantle of Mortality",
		"Mastodon Hide Mask",
		"Morrigan's Trinket of Fate",
		"Mrtyu's Rod of Disempowerment",
		"New Brood Talisman",
		"Niente's Dagger of Knowledge",
		"Nintal's Intricate Buckler",
		"Pendant of Whispers",
		"Pip's Cloak of Trickery",
		"Polished Ivory Katar",
		"Quoza's Amulet",
		"Ratalthor's Earring of Insight",
		"Redscale Cloak",
		"Restless Ice Shard",
		"Ring of True Echoes",
		"Scale-Plated Crate",
		"Scepter of the Banished",
		"Soul Banisher",
		"Starseed Bow",
		"Starsight",
		"Suspended Scale Earring",
		"Tantor's Eye",
		"Tears of the Final Stand",
		"Tusk of Frost",
		"Twilight, Staff of the Exiled",
		"Zieri's Shawl of Compassion",
	};
}
