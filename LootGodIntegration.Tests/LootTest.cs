using LootGod;
using Microsoft.AspNetCore.Mvc.Testing;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;

namespace LootGodIntegration.Tests;

public class AppFixture : IAsyncDisposable
{
	private readonly WebApplicationFactory<Program> _app = new();
	public HttpClient Client { get; private set; }

	public AppFixture()
	{
		Client = _app.CreateDefaultClient();
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync();
	}
}

public static class Extensions
{
	public static async Task EnsurePostAsJsonAsync(this HttpClient client, string requestUri)
	{
		await client.EnsurePostAsJsonAsync<string?>(requestUri, null);
	}

	public static async Task<string> EnsurePostAsJsonAsync<T>(this HttpClient client, string requestUri, T? value = default)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = value is null
				? await client.PostAsJsonAsync(requestUri, new { })
				: await client.PostAsJsonAsync(requestUri, value);

			return await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
		}
		catch
		{
			Assert.NotNull(response);
			Assert.Fail(response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync());
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}
}

public class LootTest
{
	private static async Task<string> CreateGuild(HttpClient client)
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

	private static async Task CreateZipRaidDumps(HttpClient client)
	{
		var now = DateTime.UtcNow.ToString("yyyyMMdd");
		var dump = "7\tVulak\t120\tDruid\tGroup Leader\t\t\tYes\t"u8.ToArray();
		await using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
		{
			var entry = zip.CreateEntry($"RaidRoster_firiona-{now}-210727.txt");
			await using var ent = entry.Open();
			await ent.WriteAsync(dump);
		}

		var content = new MultipartFormDataContent
		{
			{ new ByteArrayContent(stream.ToArray()), "file", "RaidRoster_firiona.zip" }
		};

		using var res = await client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	private record SsePayload<T>
	{
		public required string Evt { get; init; }
		public required T Json { get; init; }
		public required int Id { get; init; }
	}

	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

	private static async Task<SsePayload<T>> GetSsePayload<T>(HttpClient client)
	{
		var key = client.DefaultRequestHeaders.SingleOrDefault(x => x.Key == "Player-Key");
		Assert.NotEqual(default, key);
		await using var stream = await client.GetStreamAsync("/SSE?playerKey=" + key.Key);
		using var sr = new StreamReader(stream);

		Assert.Equal("data: empty", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());

		var e = await sr.ReadLineAsync();
		var json = await sr.ReadLineAsync();
		var id = await sr.ReadLineAsync();

		return new SsePayload<T>
		{
			Evt = e![7..], // event: 
			Json = JsonSerializer.Deserialize<T[]>(json![6..], _options)![0], // data:
			Id = int.Parse(id![4..]), // id:
		};
	}

	private static async Task CreateItem(HttpClient client)
	{
		const string name = "Godly Plate of the Whale";
		await client.EnsurePostAsJsonAsync("/CreateItem?name=" + name);

		var items = await client.GetFromJsonAsync<ItemDto[]>("/GetItems");
		Assert.Single(items!);
		Assert.Equal(name, items![0].Name);
		Assert.Equal(1, items![0].Id);
	}

	private static async Task CreateLootRequest(HttpClient client)
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

	private static async Task GrantLootRequest(HttpClient client)
	{
		await client.EnsurePostAsJsonAsync("/GrantLootRequest?id=1&grant=true");
	}

	[Fact]
	public async Task HealthCheck()
	{
		await using var app = new AppFixture();
		var response = await app.Client.GetStringAsync("/healthz");

		Assert.Equal("Healthy", response);
	}

	[Fact]
	public async Task CreateGuildAndLeader()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
	}

	[Fact]
	public async Task GetPlayerId()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var id = await app.Client.GetFromJsonAsync<int>("/GetPlayerId");

		Assert.Equal(1, id);
	}

	[Fact]
	public async Task GetLootLock()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var locked = await app.Client.GetFromJsonAsync<bool>("/GetLootLock");
		Assert.False(locked);

		await app.Client.EnsurePostAsJsonAsync("/ToggleLootLock?enable=true");
		var locked2 = await app.Client.GetFromJsonAsync<bool>("/GetLootLock");
		Assert.True(locked2);

		await app.Client.EnsurePostAsJsonAsync("/ToggleLootLock?enable=false");
		var locked3 = await app.Client.GetFromJsonAsync<bool>("/GetLootLock");
		Assert.False(locked3);
	}

	[Fact]
	public async Task GetAdminStatus()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var admin = await app.Client.GetFromJsonAsync<bool>("/GetAdminStatus");

		Assert.True(admin);
	}

	[Fact]
	public async Task GetLeaderStatus()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var leader = await app.Client.GetFromJsonAsync<bool>("/GetLeaderStatus");

		Assert.True(leader);
	}

	record Hooks(string Raid, string Rot);

	[Fact]
	public async Task DiscordWebhooks()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var emptyHooks = await app.Client.GetFromJsonAsync<Hooks>("/GetDiscordWebhooks");
		Assert.Empty(emptyHooks!.Raid);
		Assert.Empty(emptyHooks.Rot);

		const string domain = "https://discord.com/api/webhooks/";
		var raidHook = domain + "1/" + new string('x', 68);
		var rotHook = domain + "2/" + new string('y', 68);

		await app.Client.EnsurePostAsJsonAsync("/GuildDiscord?raidNight=true&webhook=" + raidHook);
		await app.Client.EnsurePostAsJsonAsync("/GuildDiscord?raidNight=false&webhook=" + rotHook);
		var loadedHooks = await app.Client.GetFromJsonAsync<Hooks>("/GetDiscordWebhooks");

		Assert.Equal(raidHook, loadedHooks!.Raid);
		Assert.Equal(rotHook, loadedHooks.Rot);
	}

	[Fact]
	public async Task CreateItemTest()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var emptyItems = await app.Client.GetFromJsonAsync<ItemDto[]>("/GetItems");
		Assert.Empty(emptyItems!);

		await CreateItem(app.Client);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task CreateLoot(bool raidNight)
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);

		var emptyLoots = await app.Client.GetFromJsonAsync<LootDto[]>("/GetLoots");
		Assert.Empty(emptyLoots!);

		//var sse = GetSsePayload<LootDto>(app.Client);
		var loot = new Endpoints.CreateLoot(3, 1, raidNight);
		await app.Client.EnsurePostAsJsonAsync("/UpdateLootQuantity", loot);

		var loots = await app.Client.GetFromJsonAsync<LootDto[]>("/GetLoots");
		Assert.Single(loots!);
		var dto = loots![0];
		Assert.Equal(loot.ItemId, dto.ItemId);
		Assert.Equal(loot.Quantity, raidNight ? dto.RaidQuantity : dto.RotQuantity);
		Assert.Equal(0, raidNight ? dto.RotQuantity : dto.RaidQuantity);

		//var data = await sse;
		//Assert.Equal("loots", data.Evt);
		//Assert.Equal(1, data.Id);
		//Assert.Equal(dto, data.Json);
	}

	[Fact]
	public async Task TestCreateLootRequest()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);
	}

	[Fact]
	public async Task GetLootRequests()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);

		var requests = await app.Client.GetFromJsonAsync<LootRequestDto[]>("/GetLootRequests");

		Assert.Single(requests!);
		var req = requests![0];
		Assert.Equal(1, req.Id);
		// TODO: more asserts
	}

	[Fact]
	public async Task DeleteLootRequest()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);

		await app.Client.EnsurePostAsJsonAsync("/DeleteLootRequest?id=1");
	}

	[Fact]
	public async Task TestGrantLootRequest()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);
		await GrantLootRequest(app.Client);
	}

	[Fact]
	public async Task GetGrantedLootOutput()
	{
		await using var app = new AppFixture();
		var key = await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);
		await GrantLootRequest(app.Client);

		var output = await app.Client.GetByteArrayAsync("/GetGrantedLootOutput?playerKey=" + key);

		var txt = System.Text.Encoding.UTF8.GetString(output);
		Assert.Equal("TODO", txt);
	}

	[Fact]
	public async Task FinishLootRequests()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);
		await CreateLootRequest(app.Client);
		await GrantLootRequest(app.Client);

		await app.Client.EnsurePostAsJsonAsync("/FinishLootRequests?raidNight=true");
	}

	[Fact]
	public async Task GetPasswords()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		var txt = await app.Client.GetStringAsync("/GetPasswords");
		var passwords = txt.Split(Environment.NewLine);

		Assert.Single(passwords);
		var password = passwords[0];
		Assert.StartsWith("Vulak\t", password);
		var success = Guid.TryParse(password[^36..], out var val);
		Assert.True(success);
		Assert.NotEqual(Guid.Empty, val);
	}

	[Fact]
	public async Task ImportGuildDump()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		const string dump = "Vulak\t120\tDruid\tLeader\t\t01/10/23\tPalatial Guild Hall\tMain -  Leader -  LC Admin - .Rot Loot Admin\t\ton\ton\t7344198\t01/06/23\tMain -  Leader -  LC Admin - .Rot Loot Admin\t";
		var content = new MultipartFormDataContent
		{
			{ new StringContent(dump), "file", "The_Unknown_firiona-20230111-141432.txt" }
		};

		using var res = await app.Client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	[Fact]
	public async Task ImportRaidDump()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);

		const string dump = "7\tVulak\t120\tDruid\tGroup Leader\t\t\tYes\t";
		var now = DateTime.UtcNow.ToString("yyyyMMdd");
		var content = new MultipartFormDataContent
		{
			{ new StringContent(dump), "file", $"RaidRoster_firiona-{now}-210727.txt" }
		};

		using var res = await app.Client.PostAsync("/ImportDump?offset=500", content);

		Assert.True(res.IsSuccessStatusCode);
	}

	[Fact]
	public async Task BulkImportRaidDumps()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateZipRaidDumps(app.Client);
	}

	[Theory]
	[InlineData("/GetPlayerAttendance")]
	[InlineData("/GetPlayerAttendance_V2")]
	public async Task GetRaidAttendance(string endpoint)
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateZipRaidDumps(app.Client);

		var dtos = await app.Client.GetFromJsonAsync<RaidAttendanceDto[]>(endpoint);

		Assert.Single(dtos!);
		var ra = dtos![0];
		Assert.Equal("Vulak", ra.Name);
		Assert.True(ra.Admin);
		Assert.False(ra.Hidden);
		Assert.Equal("Leader", ra.Rank);
		Assert.Equal(100, ra._30);
		Assert.Equal(100, ra._90);
		Assert.Equal(100, ra._180);
	}
}