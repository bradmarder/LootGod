using System.Security.Cryptography;

public record SpellParseOutput
{
	private readonly string _line;
	private readonly string[] _data;

	public SpellParseOutput(string line)
	{
		_line = line;
		_data = line.Split(',');
	}

	public byte[] Hash => MD5.HashData(System.Text.Encoding.UTF8.GetBytes(_line));

	public int Id => int.Parse(_data[0]); // 46876
	public string Name => _data[1][1..^1]; // "Vampiric Consumption" and remove quotes
	public string Description => _data[199].TrimStart('"').TrimEnd('"'); // "Decrease Hitpoints by 70.
	public string Description2 => _data[200].TrimStart('"').TrimEnd('"'); // Return 71.5% of damage as Hitpoints.
}
