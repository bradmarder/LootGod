using LootGod;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

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

public class LootTest()
{
	private static async Task CreateGuild(HttpClient client)
	{
		var dto = new Endpoints.CreateGuild("Vulak", "The Unknown", Server.FirionaVie);
		using var response = await client.PostAsJsonAsync("/CreateGuild", dto);
		var key = await response
			.EnsureSuccessStatusCode()
			.Content
			.ReadFromJsonAsync<Guid>();

		Assert.NotEqual(Guid.Empty, key);

		client.DefaultRequestHeaders.Add("Player-Key", key.ToString());
	}

	record SsePayload
	{
		public required string Evt { get; init; }
		public required string Json { get; init; }
		public required int Id { get; init; }
	}

	private static async Task<SsePayload> GetSSE(HttpClient client)
	{
		var key = client.DefaultRequestHeaders.Single(x => x.Key == "Player-Key").Value;
		await using var stream = await client.GetStreamAsync("/SSE?playerKey=" + key);
		using var sr = new StreamReader(stream);

		Assert.Equal("data: empty", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());

		var e = await sr.ReadLineAsync();
		var json = await sr.ReadLineAsync();
		var id = await sr.ReadLineAsync();

		return new SsePayload
		{
			Evt = e![7..], // event: 
			Json = json![6..], // data:
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

		using var _ = (await app.Client.PostAsync("/ToggleLootLock?enable=true", null)).EnsureSuccessStatusCode();
		var locked2 = await app.Client.GetFromJsonAsync<bool>("/GetLootLock");
		Assert.True(locked2);

		using var __ = (await app.Client.PostAsync("/ToggleLootLock?enable=false", null)).EnsureSuccessStatusCode();
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

		using var _ = (await app.Client.PostAsync("/GuildDiscord?raidNight=true&webhook=" + raidHook, null)).EnsureSuccessStatusCode();
		using var __ = (await app.Client.PostAsync("/GuildDiscord?raidNight=false&webhook=" + rotHook, null)).EnsureSuccessStatusCode();
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

		var sse = GetSSE(app.Client);
		var raidLoot = new Endpoints.CreateLoot(3, 1, true);
		var rotLoot = new Endpoints.CreateLoot(4, 1, false);
		using var _ = (await app.Client.PostAsJsonAsync("/UpdateLootQuantity", raidLoot)).EnsureSuccessStatusCode();
		using var __ = (await app.Client.PostAsJsonAsync("/UpdateLootQuantity", rotLoot)).EnsureSuccessStatusCode();

		var loots = await app.Client.GetFromJsonAsync<LootDto[]>("/GetLoots");
		Assert.Single(loots!);
		var loot = loots![0];
		Assert.Equal(raidLoot.ItemId, loot.ItemId);
		Assert.Equal(raidLoot.Quantity, loot.RaidQuantity);
		Assert.Equal(rotLoot.ItemId, loot.ItemId);
		Assert.Equal(rotLoot.Quantity, loot.RotQuantity);

		var data = await sse;
		Assert.Equal("loots", data.Evt);
		Assert.Equal(
"""
[{"itemId":1,"raidQuantity":3,"rotQuantity":0,"name":"Godly Plate of the Whale","isSpell":false}]
""", data.Json);
		Assert.Equal(1, data.Id);
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