public class LootTest
{
	public CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task HealthCheck()
	{
		await using var app = new AppFixture();

		var response = await app.Client.GetStringAsync("/healthz", Token);

		Assert.Equal("Healthy", response);
	}

	[Fact]
	public async Task Vacuum()
	{
		await using var app = new AppFixture();

		var value = await app.Client.EnsureGetJsonAsync<int>("/Vacuum", Token);

		Assert.Equal(0, value);
	}

	[Fact]
	public async Task DatabaseBackup()
	{
		await using var app = new AppFixture();

		var headers = await app.Client.EnsureGetHeadersAsync("/Backup?key=" + AppFixture.AdminKey, Token);

		var disposition = headers.SingleOrDefault(x => x.Key is "Content-Disposition");
		Assert.NotEqual(default, disposition);
		var values = disposition.Value.ToArray();
		Assert.Single(values);
		const string db = "lootgod-backup-1721678244.db";
		Assert.Equal($"attachment; filename={db}; filename*=UTF-8''{db}", values[0]);
	}

	[Fact]
	public async Task CreateGuild()
	{
		await using var app = new AppFixture();

		await app.Client.CreateGuildAndLeader(Token);

		var playerId = await app.Client.EnsureGetJsonAsync<int>("/GetPlayerId", Token);
		var admin = await app.Client.EnsureGetJsonAsync<bool>("/GetAdminStatus", Token);
		var leader = await app.Client.EnsureGetJsonAsync<bool>("/GetLeaderStatus", Token);
		Assert.Equal(1, playerId);
		Assert.True(admin);
		Assert.True(leader);
	}

	[Fact]
	public async Task EnableLootLock()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		// By default, LootLock will be false for all new guilds
		var defaultLootLock = await app.Client.EnsureGetJsonAsync<bool>("/GetLootLock", Token);
		Assert.False(defaultLootLock);

		var sse = app.Client.GetSsePayload<bool>(Token);
		await app.Client.EnsurePostAsJsonAsync("/ToggleLootLock", new LootLock(true), Token);
		var data = await sse;
		var lootLock = await app.Client.EnsureGetJsonAsync<bool>("/GetLootLock", Token);
		Assert.True(lootLock);
		Assert.Equal(1, data.Id);
		Assert.Equal("lock", data.Evt);
		Assert.True(data.Json);
	}

	[Theory]
	[InlineData("Welcome!")]
	public async Task MessageOfTheDay(string motd)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		var sse = app.Client.GetStringSsePayload(Token);
		await app.Client.EnsurePostAsJsonAsync("/UploadMessageOfTheDay", new MessageOfTheDay(motd), Token);
		var data = await sse;

		var dto = await app.Client.GetStringAsync("/GetMessageOfTheDay", Token);
		Assert.Equal(motd, dto);
		Assert.Equal("motd", data.Evt);
		Assert.Equal(motd, data.Json);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task DiscordWebhooks(bool raidNight)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		var emptyHooks = await app.Client.EnsureGetJsonAsync<DiscordWebhooks>("/GetDiscordWebhooks", Token);
		Assert.Empty(emptyHooks.Raid);
		Assert.Empty(emptyHooks.Rot);

		var webhook = "https://discord.com/api/webhooks/1/" + new string('x', 68);

		await app.Client.EnsurePostAsJsonAsync($"/GuildDiscord", new UpdateGuildDiscord(raidNight, webhook), Token);

		var hooks = await app.Client.EnsureGetJsonAsync<DiscordWebhooks>("/GetDiscordWebhooks", Token);
		var value = raidNight ? hooks.Raid : hooks.Rot;
		var other = raidNight ? hooks.Rot : hooks.Raid;
		Assert.Equal(webhook, value);
		Assert.Empty(other);
	}

	[Fact]
	public async Task CreateItemTest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		var emptyItems = await app.Client.EnsureGetJsonAsync<ItemSearch[]>("/GetItems", Token);
		Assert.Empty(emptyItems);

		var sse = app.Client.GetSsePayload<ItemSearch>(Token);
		await app.Client.CreateItem(Token);
		var data = await sse;

		var items = await app.Client.EnsureGetJsonAsync<ItemSearch[]>("/GetItems", Token);
		Assert.Single(items);
		var item = items.Single();
		Assert.Equal("items", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.True(item.Id == data.Json.Id);
		Assert.True(item.Name == data.Json.Name);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task CreateLoot(bool raidNight)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);

		var emptyLoots = await app.Client.EnsureGetJsonAsync<LootDto[]>("/GetLoots", Token);
		Assert.Empty(emptyLoots);

		var sse = app.Client.GetSsePayload<LootDto>(Token);
		var loot = new CreateLoot(3, itemId, raidNight);
		await app.Client.EnsurePostAsJsonAsync("/UpdateLootQuantity", loot, Token);
		var data = await sse;

		var loots = await app.Client.EnsureGetJsonAsync<LootDto[]>("/GetLoots", Token);
		Assert.Single(loots);
		var dto = loots[0];
		Assert.Equal(loot.ItemId, dto.ItemId);
		Assert.Equal(loot.Quantity, raidNight ? dto.RaidQuantity : dto.RotQuantity);
		Assert.Equal(0, raidNight ? dto.RotQuantity : dto.RaidQuantity);
		Assert.Equal("loots", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.True(dto == data.Json);
	}

	[Fact]
	public async Task TestCreateLootRequest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);

		var sse = app.Client.GetSsePayload<LootRequestDto>(Token);
		await app.Client.CreateLootRequest(itemId, Token);
		var data = await sse;

		Assert.Equal("requests", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.Equal(1, data.Json.Id);

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests", Token);
		Assert.Single(requests);
		var req = requests[0];
		Assert.True(data.Json == req);
		Assert.Equal(1, req.Id);
		Assert.False(req.Granted);
		Assert.True(req.RaidNight);
		Assert.False(req.Duplicate);

		// does not match primary class -> displays persona class when specified
		Assert.Equal(EQClass.Berserker, req.Class);
	}

	[Fact]
	public async Task DeleteLootRequest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);
		await app.Client.CreateLootRequest(itemId, Token);

		await app.Client.EnsureDeleteAsync("/DeleteLootRequest?id=1", Token);

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests", Token);
		Assert.Empty(requests);
	}

	[Fact]
	public async Task TestGrantLootRequest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);
		await app.Client.CreateLootRequest(itemId, Token);
		await app.Client.GrantLootRequest(Token);

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests", Token);
		Assert.Single(requests);
		Assert.True(requests[0].Granted);
	}

	[Fact]
	public async Task GetGrantedLootOutput()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);
		await app.Client.CreateLootRequest(itemId, Token);
		await app.Client.GrantLootRequest(Token);

		var output = await app.Client.GetStringAsync("/GetGrantedLootOutput?raidNight=true", Token);

		Assert.Equal($"{TestData.DefaultItemName} | {TestData.GuildLeader} | x1", output);
	}

	[Fact]
	public async Task FinishLootRequests()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		var itemId = await app.Client.CreateItem(Token);
		await app.Client.CreateLootRequest(itemId, Token);
		await app.Client.GrantLootRequest(Token);

		// TODO: validate discord
		await app.Client.EnsurePostAsJsonAsync("/FinishLootRequests", new FinishLoots(true), Token);

		var activeRequests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests", Token);
		var archiveItem = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetArchivedLootRequests?itemId=" + itemId, Token);
		var archiveName = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetArchivedLootRequests?name=" + TestData.GuildLeader, Token);
		Assert.Empty(activeRequests);
		Assert.Single(archiveItem);
		Assert.Single(archiveName);
		Assert.True(archiveItem[0] == archiveName[0]);
		Assert.True(archiveItem[0].Granted);

		// create another request to ensure correct lootRequest duplicate logic
		await app.Client.CreateLootRequest(itemId, Token);
		activeRequests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests", Token);
		Assert.True(activeRequests[0].Duplicate);
	}

	[Fact]
	public async Task GetPasswords()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		var txt = await app.Client.GetStringAsync("/GetPasswords", Token);

		var passwords = txt.Split(Environment.NewLine);
		Assert.Single(passwords);
		var password = passwords[0];
		Assert.StartsWith(TestData.GuildLeader, password);
		var success = Guid.TryParse(password[^36..], out var val);
		Assert.True(success);
		Assert.NotEqual(Guid.Empty, val);
	}

	[Fact]
	public async Task ImportGuildDump()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateGuildDump(Token);
	}

	[Theory]
	[InlineData($"7\t{TestData.GuildLeader}\t120\tDruid\tGroup Leader\t\t\tYes\t")]
	public async Task ImportRaidDump(string dump)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);

		const string now = "20240704";
		using var content = new MultipartFormDataContent
		{
			{ new StringContent(dump), "file", $"RaidRoster_firiona-{now}-210727.txt" }
		};

		using var res = await app.Client.PostAsync("/ImportDump?offset=500", content, Token);

		Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync(Token));
	}

	[Fact]
	public async Task BulkImportRaidDumps()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateZipRaidDumps(Token);
	}

	[Theory]
	[InlineData("Seru")]
	public async Task TransferGuildLeadership(string name)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateGuildDump(Token);

		await app.Client.EnsurePostAsJsonAsync("/TransferGuildLeadership", new TransferGuildName(name), Token);

		var leader = await app.Client.EnsureGetJsonAsync<bool>("/GetLeaderStatus", Token);
		Assert.False(leader);
	}

	[Theory]
	[InlineData("Seru")]
	public async Task LinkAlt(string altName)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateGuildDump(Token);

		await app.Client.EnsurePostAsJsonAsync("/LinkAlt?altName=" + altName, Token);

		var linkedAlts = await app.Client.EnsureGetJsonAsync<string[]>("/GetLinkedAlts", Token);
		Assert.Single(linkedAlts);
		Assert.Equal(altName, linkedAlts[0]);
	}

	[Theory]
	[InlineData(TestData.Commander)]
	public async Task Guest(string guestName)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateZipRaidDumps(Token);

		//await app.Client.EnsurePostAsJsonAsync("/Guest", new MakeGuest(guestName));

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance", Token);
		Assert.Equal(2, players.Length);
		var tormax = players.SingleOrDefault(x => x.Name == guestName);
		Assert.NotNull(tormax); // guest should appear in attendance
	}

	[Theory]
	[InlineData(TestData.CommanderId)]
	public async Task ToggleHiddenPlayer(int playerId)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateGuildDump(Token);
		await app.Client.CreateZipRaidDumps(Token);

		await app.Client.EnsurePostAsJsonAsync("/ToggleHiddenPlayer", new ToggleHiddenAdminPlayer(playerId), Token);

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance", Token);
		Assert.Equal(2, players.Length);
		var tormax = players.SingleOrDefault(x => x.Id == playerId);
		Assert.NotNull(tormax);
		Assert.True(tormax.Hidden);
	}

	[Theory]
	[InlineData(TestData.CommanderId)]
	public async Task TogglePlayerAdmin(int playerId)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateGuildDump(Token);
		await app.Client.CreateZipRaidDumps(Token);

		await app.Client.EnsurePostAsJsonAsync("/TogglePlayerAdmin", new ToggleHiddenAdminPlayer(playerId), Token);

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance", Token);
		Assert.Equal(2, players.Length);
		var tormax = players.SingleOrDefault(x => x.Id == playerId);
		Assert.NotNull(tormax);
		Assert.True(tormax.Admin);
	}

	[Theory]
	[InlineData(0, 100, 100, 100)]
	[InlineData(30, 0, 100, 100)]
	[InlineData(90, 0, 0, 100)]
	[InlineData(180, 0, 0, 0)]
	public async Task GetRaidAttendance(double futureDays, byte expected30, byte expected90, byte expected180)
	{
		await using var app = new AppFixture(futureDays);
		await app.Client.CreateGuildAndLeader(Token);
		await app.Client.CreateZipRaidDumps(Token);

		var dtos = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance", Token);

		// if there is zero RA for past 180 days, the player/leader will not even appear
		if (expected180 is 0)
		{
			Assert.Empty(dtos);
			return;
		}

		//Assert.Single(dtos); // TODO: guest issue
		var ra = dtos[1];
		Assert.Equal(1, ra.Id);
		Assert.Equal(TestData.GuildLeader, ra.Name);
		Assert.True(ra.Admin);
		Assert.False(ra.Hidden);
		Assert.Equal(Rank.Leader, ra.Rank);
		Assert.Null(ra.LastOnDate);
		Assert.Equal(EQClass.Bard, ra.Class);
		Assert.Null(ra.Notes);
		//Assert.Equal(TestData.Zone, ra.Zone);
		Assert.Empty(ra.Alts);
		Assert.Null(ra.Level);
		Assert.Equal(0, ra.T1GrantedLootCount);
		Assert.Equal(0, ra.T2GrantedLootCount);
		Assert.Equal(expected30, ra._30);
		Assert.Equal(expected90, ra._90);
		Assert.Equal(expected180, ra._180);
	}

	[Fact]
	public async Task DataSync()
	{
		await using var app = new AppFixture();

		await app.Client.EnsurePostAsJsonAsync("/DataSync", Token);

		var items = await app.Client.EnsureGetJsonAsync<ItemSearch[]>("/GetItems", Token);
		Assert.NotEmpty(items);
	}
}
