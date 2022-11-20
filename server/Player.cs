using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(Name), IsUnique = true)]
public class Player
{
	public static readonly IReadOnlyDictionary<string, EQClass> ClassNameToEnumMap = new Dictionary<string, EQClass>
	{
		["Bard"] = EQClass.Bard,
		["Beastlord"] = EQClass.Beastlord,
		["Berserker"] = EQClass.Berserker,
		["Cleric"] = EQClass.Cleric,
		["Druid"] = EQClass.Druid,
		["Enchanter"] = EQClass.Enchanter,
		["Magician"] = EQClass.Magician,
		["Monk"] = EQClass.Monk,
		["Necromancer"] = EQClass.Necromancer,
		["Paladin"] = EQClass.Paladin,
		["Ranger"] = EQClass.Ranger,
		["Rogue"] = EQClass.Rogue,
		["Shadow"] = EQClass.Shadowknight,
		["Shaman"] = EQClass.Shaman,
		["Warrior"] = EQClass.Warrior,
		["Wizard"] = EQClass.Wizard,
	};

	public Player() { }
	public Player(string name, string eqClass)
	{
		Name = name;
		Class = ClassNameToEnumMap[eqClass];
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	[Required]
	[MaxLength(24)]
	public string Name { get; set; } = null!;

	public EQClass Class { get; set; }

	/// <summary>
	/// Nullable by default, because this is not known at the time when entered into the DB
	/// </summary>
	public int? RankId { get; set; }

	/// <summary>
	/// Only admins may see hidden players in the Raid Attendance table
	/// </summary>
	public bool Hidden { get; set; }

	[ForeignKey(nameof(RankId))]
	public virtual Rank? Rank { get; set; } = null!;

	[InverseProperty(nameof(RaidDump.Player))]
	public virtual ICollection<RaidDump> RaidDumps { get; } = null!;
}
