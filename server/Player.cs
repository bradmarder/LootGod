using Microsoft.EntityFrameworkCore;
using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace LootGod;

[Index(nameof(GuildId), nameof(Name), IsUnique = true)]
[Index(nameof(Key), IsUnique = true)]
public class Player
{
	public static readonly FrozenDictionary<string, EQClass> _classNameToEnumMap = new Dictionary<string, EQClass>
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
	}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	static Guid GetRandomGuid()
	{
		Span<byte> span = stackalloc byte[16];
		RandomNumberGenerator.Fill(span);
		return new Guid(span);
	}

	private Player() { }

	/// <summary>
	/// Guild leader signup ctor
	/// </summary>
	public Player(string leaderName, string guildName, Server server)
	{
		Name = leaderName;
		Active = true;
		Admin = true;
		Key = GetRandomGuid();
		Class = EQClass.Bard; // TODO: FIX THIS!!! class isn't updated ever
		Guild = new(guildName, server);
		Rank = new("Leader", Guild);
	}

	/// <summary>
	/// Raid dump ctor
	/// </summary>
	public Player(string name, string eqClass, int guildId)
	{
		Name = name;
		Class = _classNameToEnumMap[eqClass];
		GuildId = guildId;
		Key = GetRandomGuid();
	}

	/// <summary>
	/// Guild dump ctor
	/// </summary>
	public Player(GuildDumpPlayerOutput dump, int guildId)
	{
		GuildId = guildId;
		Active = true;
		Name = dump.Name;
		Class = _classNameToEnumMap[dump.Class];
		Alt = dump.Alt;
		Level = dump.Level;
		LastOnDate = dump.LastOnDate;
		Notes = dump.Notes;
		Key = GetRandomGuid();
	}

	[Key]
	public int Id { get; set; }

	public bool Admin { get; set; }

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

	public long CreatedDate { get; set; }

	[Required]
	[StringLength(24, MinimumLength = 4)]
	public string Name { get; set; } = null!;

	public string? Notes { get; set; }

	public EQClass Class { get; set; }

	/// <summary>
	/// Nullable by default, because this may not known at the time when entered into the DB (due to a raid dump)
	/// </summary>
	public int? RankId { get; set; }

	/// <summary>
	/// Only admins may see hidden players in the Raid Attendance table
	/// </summary>
	public bool Hidden { get; set; }

	public int? MainId { get; set; }

	[ForeignKey(nameof(MainId))]
	public virtual Player? Main { get; set; }

	[ForeignKey(nameof(RankId))]
	public virtual Rank? Rank { get; set; }

	[ForeignKey(nameof(GuildId))]
	public virtual Guild Guild { get; set; } = null!;

	[InverseProperty(nameof(RaidDump.Player))]
	public virtual List<RaidDump> RaidDumps { get; } = new();

	[InverseProperty(nameof(LootRequest.Player))]
	public virtual List<LootRequest> LootRequests { get; } = new();

	[InverseProperty(nameof(Main))]
	public virtual List<Player> Alts { get; } = new();
}
