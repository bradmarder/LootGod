using LootGod;
using Microsoft.AspNetCore.Mvc.Testing;
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
		catch (Exception ex)
		{
			if (response is null) { throw; }

			var content = await response.Content.ReadAsStringAsync();
			throw new Exception(ex.ToString() + Environment.NewLine + content);
		}
		finally
		{
			response?.Dispose();
		}
	}
}

public class LootTest
{
	private static async Task CreateGuild(HttpClient client)
	{
		var dto = new Endpoints.CreateGuild("Vulak", "The Unknown", Server.FirionaVie);
		var json = await client.EnsurePostAsJsonAsync("/CreateGuild", dto);
		var key = json[1..^1];
		var success = Guid.TryParse(key, out var pKey);

		Assert.True(success);
		Assert.NotEqual(Guid.Empty, pKey);

		client.DefaultRequestHeaders.Add("Player-Key", key);
	}

	record SsePayload<T>
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
		using var _ = (await client.PostAsync("/CreateItem?name=" + name, null)).EnsureSuccessStatusCode();

		var items = await client.GetFromJsonAsync<ItemDto[]>("/GetItems");
		Assert.Single(items!);
		Assert.Equal(name, items![0].Name);
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

	[Fact]
	public async Task CreateLoot()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
		await CreateItem(app.Client);

		var emptyLoots = await app.Client.GetFromJsonAsync<LootDto[]>("/GetLoots");
		Assert.Empty(emptyLoots!);

		var sse = GetSsePayload<LootDto>(app.Client);
		var raidLoot = new Endpoints.CreateLoot(3, 1, true);
		var rotLoot = new Endpoints.CreateLoot(4, 1, false);
		await app.Client.EnsurePostAsJsonAsync("/UpdateLootQuantity", raidLoot);
		await app.Client.EnsurePostAsJsonAsync("/UpdateLootQuantity", rotLoot);

		var loots = await app.Client.GetFromJsonAsync<LootDto[]>("/GetLoots");
		Assert.Single(loots!);
		var loot = loots![0];
		Assert.Equal(raidLoot.ItemId, loot.ItemId);
		Assert.Equal(raidLoot.Quantity, loot.RaidQuantity);
		Assert.Equal(rotLoot.ItemId, loot.ItemId);
		Assert.Equal(rotLoot.Quantity, loot.RotQuantity);

		var data = await sse;
		Assert.Equal("loots", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.Equal(1, data.Json.ItemId);
		Assert.Equal(3, data.Json.RaidQuantity);
		Assert.Equal(0, data.Json.RotQuantity);
		Assert.Equal("Godly Plate of the Whale", data.Json.Name);
		Assert.False(data.Json.IsSpell);
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
		Assert.True(Guid.TryParse(password[^36..], out _));
	}

	[Fact]
	public async Task ImportDumps()
	{
		await using var app = new AppFixture();
		await CreateGuild(app.Client);
	}
}