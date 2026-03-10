using System.Collections.Frozen;
using System.Security.Cryptography;

public record SpellParseOutput
{
	public static readonly FrozenDictionary<string, EQClass> ClassNameToEnumMap = new Dictionary<string, EQClass>
	{
		["BRD"] = EQClass.Bard,
		["BST"] = EQClass.Beastlord,
		["BER"] = EQClass.Berserker,
		["CLR"] = EQClass.Cleric,
		["DRU"] = EQClass.Druid,
		["ENC"] = EQClass.Enchanter,
		["MAG"] = EQClass.Magician,
		["MNK"] = EQClass.Monk,
		["NEC"] = EQClass.Necromancer,
		["PAL"] = EQClass.Paladin,
		["RNG"] = EQClass.Ranger,
		["ROG"] = EQClass.Rogue,
		["SHD"] = EQClass.Shadowknight,
		["SHM"] = EQClass.Shaman,
		["WAR"] = EQClass.Warrior,
		["WIZ"] = EQClass.Wizard,
	}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	private readonly string _line;
	private readonly string[] _data;

	public SpellParseOutput(string line)
	{
		_line = line;
		_data = line.Split(',');
	}

	public byte[] Hash => MD5.HashData(System.Text.Encoding.UTF8.GetBytes(_line));

	public int Id => int.Parse(_data[0]); // 46876
	public bool Rank3 => Name.Contains(" Rk. III");
	private string ClassLevel => _data[193]; // multiple formats, could be "35 NEC" or "RNG/129"
	public byte? Level => ClassLevel.Contains('/') && !ClassLevel.Contains(' ')
		? byte.Parse(ClassLevel.Split('/')[1])
		: null;
	public EQClass? Class => ClassLevel.Contains('/') && !ClassLevel.Contains(' ')
		? ClassNameToEnumMap[ClassLevel.Split('/')[0]]
		: null;
	public string Name => _data[1][1..^1]; // "Vampiric Consumption" and remove quotes
	public string Description => _data[199].TrimStart('"').TrimEnd('"'); // "Decrease Hitpoints by 70.
	public string Description2 => _data[200].TrimStart('"').TrimEnd('"'); // Return 71.5% of damage as Hitpoints.
	public bool IsRaid => Rank3 && Level > 125;
}