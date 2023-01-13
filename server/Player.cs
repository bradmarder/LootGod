using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace LootGod;

[Index(nameof(Name), nameof(GuildId), IsUnique = true)]
[Index(nameof(GuildId))]
[Index(nameof(Key), IsUnique = true)]
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
		["Shadow Knight"] = EQClass.Shadowknight,
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
	public Player(GuildDumpPlayerOutput dump, int guildId)
	{
		GuildId = guildId;
		Active = true;
		Name = dump.Name;
		Class = ClassNameToEnumMap[dump.Class];
		Alt = dump.Alt;
		Level = dump.Level;
		LastOnDate = dump.LastOnDate;

		if (!dump.Alt)
		{
			Span<byte> span = stackalloc byte[16];
			RandomNumberGenerator.Fill(span);
			Key = new Guid(span);
		}
	}

	[Key]
	public int Id { get; set; }

	public bool Admin { get; set; }

	[StringLength(255)]
	public string? Email { get; set; }

	/// <summary>
	/// Alts are never given keys
	/// </summary>
	public Guid? Key { get; set; }

	public byte? Level { get; set; }
	public bool? Alt { get; set; }

	/// <summary>
	/// default true, set to false if guild dump with no player
	/// </summary>
	public bool? Active { get; set; }
	public DateOnly? LastOnDate { get; set; }

	public int GuildId { get; set; }
	
	// or multiple guilds with IsActive?
	// unique by Name/Guild/Server
	// Guild/Admin is one entity?

	public DateTime CreatedDate { get; set; }

	[Required]
	[MaxLength(24)]
	public string Name { get; set; } = null!;

	public EQClass Class { get; set; }

	/// <summary>
	/// Nullable by default, because this may not known at the time when entered into the DB (due to a raid dump)
	/// </summary>
	public int? RankId { get; set; }

	/// <summary>
	/// Only admins may see hidden players in the Raid Attendance table
	/// </summary>
	public bool Hidden { get; set; }

	[ForeignKey(nameof(RankId))]
	public virtual Rank? Rank { get; set; } = null!;

	[ForeignKey(nameof(GuildId))]
	public virtual Guild Guild { get; set; } = null!;

	[InverseProperty(nameof(RaidDump.Player))]
	public virtual ICollection<RaidDump> RaidDumps { get; } = null!;

	[InverseProperty(nameof(LootRequest.Player))]
	public virtual ICollection<LootRequest> LootRequests { get; } = null!;
}
