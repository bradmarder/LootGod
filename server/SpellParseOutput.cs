using System.Security.Cryptography;
using System.Text.RegularExpressions;

public partial record SpellParseOutput
{
	[GeneratedRegex(",(?=(?:[^\"]*\\\"[^\"]*\\\")*[^\\\"-]*$)")]
	private static partial Regex SplitByCommaConsideringQuotes();

	private readonly string _line;
	private readonly string[] _data;

	public SpellParseOutput(string line)
	{
		_line = line;
		_data = SplitByCommaConsideringQuotes().Split(line);
	}

	public byte[] Hash => MD5.HashData(System.Text.Encoding.UTF8.GetBytes(_line));

	public int Id => int.Parse(_data[0]); // 46876
	public bool Rank3 => Name.EndsWith(" Rk. III");
	public string ClassLevel => _data[193].Replace("\"", ""); // classes may have different level requirements, SHM/127 MAG/126
	public byte MaxLevel => ClassLevel is "None"
		? byte.MinValue
		: ClassLevel
			.Split(' ')
			.Select(x => byte.Parse(x.Split('/')[1]))
			.Max();
	public string Name => _data[1][1..^1]; // "Vampiric Consumption" and remove quotes
	public string Description => _data[199].TrimStart('"').TrimEnd('"'); // "Decrease Hitpoints by 70.
	public string Description2 => _data[200].TrimStart('"').TrimEnd('"'); // Return 71.5% of damage as Hitpoints.
	public bool IsRaid => Rank3 && MaxLevel > 125 && MaxLevel < byte.MaxValue;
}