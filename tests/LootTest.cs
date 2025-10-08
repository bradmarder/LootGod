public class LootTest
{
	[Fact]
	public async Task HealthCheck()
	{
		await using var app = new AppFixture();

		var response = await app.Client.GetStringAsync("/healthz");

		Assert.Equal("Healthy", response);
	}

	[Fact]
	public async Task Vacuum()
	{
		await using var app = new AppFixture();

		var value = await app.Client.EnsureGetJsonAsync<int>("/Vacuum");

		Assert.Equal(0, value);
	}

	[Fact]
	public async Task DatabaseBackup()
	{
		await using var app = new AppFixture();

		var headers = await app.Client.EnsureGetHeadersAsync("/Backup?key=" + AppFixture.AdminKey);

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

		await app.Client.CreateGuildAndLeader();

		var id = await app.Client.EnsureGetJsonAsync<int>("/GetPlayerId");
		var admin = await app.Client.EnsureGetJsonAsync<bool>("/GetAdminStatus");
		var leader = await app.Client.EnsureGetJsonAsync<bool>("/GetLeaderStatus");
		Assert.Equal(1, id);
		Assert.True(admin);
		Assert.True(leader);
	}

	[Fact]
	public async Task EnableLootLock()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();

		// By default, LootLock will be false for all new guilds
		var defaultLootLock = await app.Client.EnsureGetJsonAsync<bool>("/GetLootLock");
		Assert.False(defaultLootLock);

		var sse = app.Client.GetSsePayload<bool>();
		await app.Client.EnsurePostAsJsonAsync("/ToggleLootLock", new LootLock(true));
		var data = await sse;

		var lootLock = await app.Client.EnsureGetJsonAsync<bool>("/GetLootLock");
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
		await app.Client.CreateGuildAndLeader();

		var sse = app.Client.GetStringSsePayload();
		await app.Client.EnsurePostAsJsonAsync("/UploadMessageOfTheDay", new MessageOfTheDay(motd));
		var data = await sse;

		var dto = await app.Client.GetStringAsync("/GetMessageOfTheDay");
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
		await app.Client.CreateGuildAndLeader();

		var emptyHooks = await app.Client.EnsureGetJsonAsync<DiscordWebhooks>("/GetDiscordWebhooks");
		Assert.Empty(emptyHooks.Raid);
		Assert.Empty(emptyHooks.Rot);

		var webhook = "https://discord.com/api/webhooks/1/" + new string('x', 68);

		await app.Client.EnsurePostAsJsonAsync($"/GuildDiscord", new UpdateGuildDiscord(raidNight, webhook));

		var hooks = await app.Client.EnsureGetJsonAsync<DiscordWebhooks>("/GetDiscordWebhooks");
		var value = raidNight ? hooks.Raid : hooks.Rot;
		var other = raidNight ? hooks.Rot : hooks.Raid;
		Assert.Equal(webhook, value);
		Assert.Empty(other);
	}

	[Fact]
	public async Task CreateItemTest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();

		var emptyItems = await app.Client.EnsureGetJsonAsync<ItemDto[]>("/GetItems");
		Assert.Empty(emptyItems);

		var sse = app.Client.GetSsePayload<ItemDto>();
		await app.Client.CreateItem();
		var data = await sse;

		var items = await app.Client.EnsureGetJsonAsync<ItemDto[]>("/GetItems");
		Assert.Single(items);
		var item = items[0];
		Assert.Equal("items", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.Equal(1, data.Json.Id);
		Assert.True(item == data.Json);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task CreateLoot(bool raidNight)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();

		var emptyLoots = await app.Client.EnsureGetJsonAsync<LootDto[]>("/GetLoots");
		Assert.Empty(emptyLoots);

		var sse = app.Client.GetSsePayload<LootDto>();
		var loot = new CreateLoot(3, 1, raidNight);
		await app.Client.EnsurePostAsJsonAsync("/UpdateLootQuantity", loot);
		var data = await sse;

		var loots = await app.Client.EnsureGetJsonAsync<LootDto[]>("/GetLoots");
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
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();

		var sse = app.Client.GetSsePayload<LootRequestDto>();
		await app.Client.CreateLootRequest();
		var data = await sse;

		Assert.Equal("requests", data.Evt);
		Assert.Equal(1, data.Id);
		Assert.Equal(1, data.Json.Id);

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests");
		Assert.Single(requests);
		var req = requests[0];
		Assert.True(data.Json == req);
		Assert.Equal(1, req.Id);
		Assert.False(req.Granted);
		Assert.True(req.RaidNight);

		// does not match primary class -> displays persona class when specified
		Assert.Equal(EQClass.Berserker, req.Class);
	}

	[Fact]
	public async Task DeleteLootRequest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();
		await app.Client.CreateLootRequest();

		await app.Client.EnsureDeleteAsync("/DeleteLootRequest?id=1");

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests");
		Assert.Empty(requests);
	}

	[Fact]
	public async Task TestGrantLootRequest()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();
		await app.Client.CreateLootRequest();
		await app.Client.GrantLootRequest();

		var requests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests");
		Assert.Single(requests);
		Assert.True(requests[0].Granted);
	}

	[Fact]
	public async Task GetGrantedLootOutput()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();
		await app.Client.CreateLootRequest();
		await app.Client.GrantLootRequest();

		var output = await app.Client.GetStringAsync("/GetGrantedLootOutput?raidNight=true");

		Assert.Equal($"{TestData.DefaultItemName} | {TestData.GuildLeader} | x1", output);
	}

	[Fact]
	public async Task FinishLootRequests()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateItem();
		await app.Client.CreateLootRequest();
		await app.Client.GrantLootRequest();

		// TODO: validate discord
		await app.Client.EnsurePostAsJsonAsync("/FinishLootRequests", new FinishLoots(true));

		var activeRequests = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetLootRequests");
		var archiveItem = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetArchivedLootRequests?itemId=1");
		var archiveName = await app.Client.EnsureGetJsonAsync<LootRequestDto[]>("/GetArchivedLootRequests?name=" + TestData.GuildLeader);
		Assert.Empty(activeRequests);
		Assert.Single(archiveItem);
		Assert.Single(archiveName);
		Assert.True(archiveItem[0] == archiveName[0]);
		Assert.True(archiveItem[0].Granted);
	}

	[Fact]
	public async Task GetPasswords()
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();

		var txt = await app.Client.GetStringAsync("/GetPasswords");

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
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateGuildDump();
	}

	[Theory]
	[InlineData($"7\t{TestData.GuildLeader}\t120\tDruid\tGroup Leader\t\t\tYes\t")]
	public async Task ImportRaidDump(string dump)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();

		const string now = "20240704";
		using var content = new MultipartFormDataContent
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
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateZipRaidDumps();
	}

	[Theory]
	[InlineData("Seru")]
	public async Task TransferGuildLeadership(string name)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateGuildDump();

		await app.Client.EnsurePostAsJsonAsync("/TransferGuildLeadership", new TransferGuildName(name));

		var leader = await app.Client.EnsureGetJsonAsync<bool>("/GetLeaderStatus");
		Assert.False(leader);
	}

	[Theory]
	[InlineData("Seru")]
	public async Task LinkAlt(string altName)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateGuildDump();

		await app.Client.EnsurePostAsJsonAsync("/LinkAlt?altName=" + altName);

		var linkedAlts = await app.Client.EnsureGetJsonAsync<string[]>("/GetLinkedAlts");
		Assert.Single(linkedAlts);
		Assert.Equal(altName, linkedAlts[0]);
	}

	[Theory]
	[InlineData(TestData.Commander)]
	public async Task Guest(string guestName)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateZipRaidDumps();

		//await app.Client.EnsurePostAsJsonAsync("/Guest", new MakeGuest(guestName));

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance");
		Assert.Equal(2, players.Length);
		var tormax = players.SingleOrDefault(x => x.Name == guestName);
		Assert.NotNull(tormax); // guest should appear in attendance
	}

	[Theory]
	[InlineData(TestData.CommanderId)]
	public async Task ToggleHiddenPlayer(int playerId)
	{
		await using var app = new AppFixture();
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateGuildDump();
		await app.Client.CreateZipRaidDumps();

		await app.Client.EnsurePostAsJsonAsync("/ToggleHiddenPlayer", new ToggleHiddenAdminPlayer(playerId));

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance");
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
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateGuildDump();
		await app.Client.CreateZipRaidDumps();

		await app.Client.EnsurePostAsJsonAsync("/TogglePlayerAdmin", new ToggleHiddenAdminPlayer(playerId));

		var players = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance");
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
		await app.Client.CreateGuildAndLeader();
		await app.Client.CreateZipRaidDumps();

		var dtos = await app.Client.EnsureGetJsonAsync<RaidAttendanceDto[]>("/GetPlayerAttendance");

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
}
