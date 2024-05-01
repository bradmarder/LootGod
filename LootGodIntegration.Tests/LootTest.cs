using LootGod;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace LootGodIntegration.Tests;

public class AppFixture : IAsyncDisposable
{
	private readonly WebApplicationFactory<Program> _app;
	public HttpClient Client { get; private set; }

	public AppFixture()
	{
		_app = new();
		Client = _app.CreateDefaultClient();
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync();
	}
}

[TestCaseOrderer("LootGodIntegration.Tests.PriorityOrderer", "LootGodIntegration.Tests")]
public class LootTest(AppFixture _fixture) : IClassFixture<AppFixture>
{
	private HttpClient Client => _fixture.Client;

	[Fact, TestPriority(0)]
	public async Task HealthCheck()
	{
		var response = await Client.GetStringAsync("/healthz");

		Assert.Equal("Healthy", response);
	}

	[Fact, TestPriority(1)]
	public async Task CreateGuild()
	{
		var dto = new Endpoints.CreateGuild("Vulak", "The Unknown", Server.FirionaVie);
		using var response = await Client.PostAsJsonAsync("/CreateGuild", dto);
		var key = await response
			.EnsureSuccessStatusCode()
			.Content
			.ReadFromJsonAsync<Guid>();

		Assert.NotEqual(Guid.Empty, key);

		Client.DefaultRequestHeaders.Add("Player-Key", key.ToString());
	}

	[Fact, TestPriority(2)]
	public async Task GetLootLock()
	{
		var locked = await Client.GetFromJsonAsync<bool>("/GetLootLock");

		Assert.False(locked);
	}

	[Fact, TestPriority(3)]
	public async Task GetPlayerId()
	{
		var id = await Client.GetFromJsonAsync<int>("/GetPlayerId");

		Assert.Equal(1, id);
	}

	[Fact, TestPriority(4)]
	public async Task GetAdminStatus()
	{
		var admin = await Client.GetFromJsonAsync<bool>("/GetAdminStatus");

		Assert.True(admin);
	}

	[Fact, TestPriority(5)]
	public async Task GetLeaderStatus()
	{
		var leader = await Client.GetFromJsonAsync<bool>("/GetLeaderStatus");

		Assert.True(leader);
	}

	record Hooks(string Raid, string Rot);

	[Fact, TestPriority(6)]
	public async Task DiscordWebhooks()
	{
		var emptyHooks = await Client.GetFromJsonAsync<Hooks>("/GetDiscordWebhooks");
		Assert.Empty(emptyHooks!.Raid);
		Assert.Empty(emptyHooks.Rot);

		const string domain = "https://discord.com/api/webhooks/";
		var raidHook = domain + "1/" + new string('x', 68);
		var rotHook = domain + "2/" + new string('y', 68);

		using var _ = (await Client.PostAsync("/GuildDiscord?raidNight=true&webhook=" + raidHook, null)).EnsureSuccessStatusCode();
		using var __ = (await Client.PostAsync("/GuildDiscord?raidNight=false&webhook=" + rotHook, null)).EnsureSuccessStatusCode();
		var loadedHooks = await Client.GetFromJsonAsync<Hooks>("/GetDiscordWebhooks");

		Assert.Equal(raidHook, loadedHooks!.Raid);
		Assert.Equal(rotHook, loadedHooks.Rot);
	}
}