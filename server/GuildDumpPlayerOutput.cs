public record GuildDumpPlayerOutput(string[] _output)
{
	public string Name => _output[0];
	public byte Level => byte.Parse(_output[1]);
	public string Class => _output[2];
	public string Rank => _output[3];
	public bool Alt => _output[4] is "A";
	public DateOnly LastOnDate => DateOnly.ParseExact(_output[5], "MM/dd/yy");
	public string Zone => _output[6];
	public string Notes => _output[13];
}
