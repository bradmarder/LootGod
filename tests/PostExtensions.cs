using System.IO.Compression;
using System.Text;

public static class PostExtensions
{
	public static async Task<string> CreateGuildAndLeader(this HttpClient client)
	{
		var dto = new CreateGuild(TestData.GuildLeader, "The Unknown", Server.FirionaVie);
		var json = await client.EnsurePostAsJsonAsync("/CreateGuild", dto);
		var key = json[1..^1];
		var success = Guid.TryParse(key, out var pKey);

		Assert.True(success, json);
		Assert.NotEqual(Guid.Empty, pKey);

		client.DefaultRequestHeaders.Add("Player-Key", key);

		return key;
	}

	public static async Task CreateZipRaidDumps(this HttpClient client)
	{
		const string vulak = $"7\t{TestData.GuildLeader}\t120\tDruid\tGroup Leader\t\t\tYes\t";
		const string tormax = $"7\t{TestData.Commander}\t120\tWizard\tGroup Leader\t\t\tYes\t";
		var lines = string.Join(Environment.NewLine, [vulak, tormax]);
		var dump = Encoding.UTF8.GetBytes(lines);

		await using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
		{
			const string now = "20240704";
			var entry = zip.CreateEntry($"RaidRoster_firiona-{now}-210727.txt");
			await using var ent = entry.Open();
			await ent.WriteAsync(dump);
		}

		using var content = new MultipartFormDataContent
		{
			{ new ByteArrayContent(stream.ToArray()), "file", "RaidRoster_firiona.zip" }
		};

		using var res = await client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	public static async Task CreateGuildDump(this HttpClient client)
	{
		const string vulak = $"{TestData.GuildLeader}\t120\tDruid\t{Rank.Leader}\t\t01/10/23\tHall\tMain -  Leader\t\ton\ton\t7344198\t01/06/23\tMain - Leader\t";
		var seru = vulak.Replace(TestData.GuildLeader, "Seru").Replace(Rank.Leader, "Knight").Replace("\t\t01/10/23", "\tA\t01/10/23");
		var tormax = vulak.Replace(TestData.GuildLeader, TestData.Commander).Replace(Rank.Leader, "Knight");
		var dump = string.Join(Environment.NewLine, [vulak, seru, tormax]);
		using var content = new MultipartFormDataContent
		{
			{ new StringContent(dump), "file", "The_Unknown_firiona-20230111-141432.txt" }
		};

		using var res = await client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	public static async Task CreateItem(this HttpClient client)
	{
		await client.EnsurePostAsJsonAsync("/CreateItem?name=" + TestData.DefaultItemName);

		var items = await client.EnsureGetJsonAsync<ItemDto[]>("/GetItems");
		Assert.Single(items);
		var item = items[0];
		Assert.Equal(TestData.DefaultItemName, item.Name);
		Assert.Equal(1, item.Id);
	}

	public static async Task CreateLootRequest(this HttpClient client)
	{
		var dto = new CreateLootRequest
		{
			ItemId = 1,
			Class = EQClass.Berserker,
			CurrentItem = "Rusty Axe",
			Quantity = 1,
			RaidNight = true,
		};
		await client.EnsurePostAsJsonAsync("/CreateLootRequest", dto);
	}

	public static async Task GrantLootRequest(this HttpClient client)
	{
		await client.EnsurePostAsJsonAsync($"/GrantLootRequest?id={1}&grant={true}");
	}
}
