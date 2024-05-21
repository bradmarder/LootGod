using LootGod;
using System.IO.Compression;

namespace LootGodIntegration.Tests;

public static class PostExtensions
{
	public static async Task<string> CreateGuildAndLeader(this HttpClient client)
	{
		var dto = new Endpoints.CreateGuild("Vulak", "The Unknown", Server.FirionaVie);
		var json = await client.EnsurePostAsJsonAsync("/CreateGuild", dto);
		var key = json[1..^1];
		var success = Guid.TryParse(key, out var pKey);

		Assert.True(success);
		Assert.NotEqual(Guid.Empty, pKey);

		client.DefaultRequestHeaders.Add("Player-Key", key);

		return key;
	}

	//private static async Task CreateZipDump(ZipArchive zip, byte[] dump)
	//{
	//	var now = DateTime.UtcNow.ToString("yyyyMMdd");
	//	var entry = zip.CreateEntry($"RaidRoster_firiona-{now}-210727.txt");
	//	await using var ent = entry.Open();
	//	await ent.WriteAsync(dump);
	//	await ent.DisposeAsync();
	//}

	public static async Task CreateZipRaidDumps(this HttpClient client)
	{
		var leader = "7\tVulak\t120\tDruid\tGroup Leader\t\t\tYes\t"u8.ToArray();
		var newbie = "7\tTormax\t120\tWizard\tGroup Leader\t\t\tYes\t"u8.ToArray();
		var dumps = new[] { leader, newbie };

		await using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
		{
			var now = DateTime.UtcNow.ToString("yyyyMMdd");
			var entry = zip.CreateEntry($"RaidRoster_firiona-{now}-210727.txt");
			await using var ent = entry.Open();
			foreach (var x in dumps)
			{
				await ent.WriteAsync(x);
			}
			await ent.DisposeAsync();
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
		const string line = "Vulak\t120\tDruid\tLeader\t\t01/10/23\tPalatial Guild Hall\tMain -  Leader -  LC Admin - .Rot Loot Admin\t\ton\ton\t7344198\t01/06/23\tMain -  Leader -  LC Admin - .Rot Loot Admin\t";
		var dump = line + Environment.NewLine + line.Replace("Vulak", "Seru").Replace("Leader", "Knight").Replace("\t\t01/10/23", "\tA\t01/10/23");
		using var content = new MultipartFormDataContent
		{
			{ new StringContent(dump), "file", "The_Unknown_firiona-20230111-141432.txt" }
		};

		using var res = await client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	public static async Task CreateItem(this HttpClient client)
	{
		const string name = "Godly Plate of the Whale";
		await client.EnsurePostAsJsonAsync("/CreateItem?name=" + name);

		var items = await client.EnsureGetJsonAsync<ItemDto[]>("/GetItems");
		Assert.Single(items);
		var item = items[0];
		Assert.Equal(name, item.Name);
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
