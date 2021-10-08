using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var path = Path.Combine(home, "lootgod.db");
builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite($"Data Source={path};"));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("GetLootRequests", async (context) =>
{
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var requests = await db.LootRequests.ToListAsync();
	await context.Response.WriteAsJsonAsync(requests);
});

app.MapPost("CreateLoot", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLoot>();
	ArgumentNullException.ThrowIfNull(dto);
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	_ = db.Loots.Add(new(dto));
	_ = await db.SaveChangesAsync();
});

app.MapPost("CreateLootRequest", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLootRequest>();
	ArgumentNullException.ThrowIfNull(dto);
	var exists = context.Request.Headers.TryGetValue("key", out var key);

	if (!exists) { throw new UnauthorizedAccessException(); }

	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var player = await db.Players.SingleAsync(x => x.Key == key);
	var ip = context.Connection.RemoteIpAddress?.ToString();
	_ = db.LootRequests.Add(new(dto, player.Id, ip));
	_ = await db.SaveChangesAsync();
});

app.MapPost("CreatePlayer", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreatePlayer>();
	ArgumentNullException.ThrowIfNull(dto);
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	var player = new Player(dto);
	_ = db.Players.Add(player);
	_ = await db.SaveChangesAsync();

	await context.Response.WriteAsync(player.Key);
});

app.Run();

public class LootGodContext : DbContext
{
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Loot> Loots => Set<Loot>();
	public DbSet<Player> Players => Set<Player>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder
			.Entity<LootRequest>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<Player>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder.Entity<Player>(entity =>
		{
			entity.HasIndex(e => e.Key).IsUnique();
		});
	}
}
public class CreatePlayer
{
	public string Name { get; set; } = null!;
}
public class Player
{
	private static string GenerateRandomKey()
	{
		Span<byte> bytes = stackalloc byte[16];
		RandomNumberGenerator.Fill(bytes);
		return new Guid(bytes).ToString("n").Substring(0, 12);
	}

	public Player(CreatePlayer dto)
	{
		CharacterName = dto.Name;
		Key = GenerateRandomKey();
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	[MaxLength(12)]
	public string Key { get; set; }

	[MaxLength(24)]
	public string CharacterName { get; set; }

	[InverseProperty(nameof(LootRequest.Player))]
	public virtual ICollection<LootRequest> LootRequests { get; } = null!;
}
public class LootRequest
{
	public LootRequest(CreateLootRequest dto, int playerId, string? ip)
	{
		CreatedDate = DateTime.UtcNow;
		PlayerId = playerId;
		IP = ip;
		CharacterName = dto.CharacterName;
		IsMain = dto.IsMain;
		Class = dto.Class;
		LootId = dto.LootId;
		Quantity = dto.Quantity;
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	public string? IP { get; set; }

	[MaxLength(24)]
	public string CharacterName { get; set; }

	public bool IsMain { get; set; }

	public EQClass Class { get; set; }

	public int LootId { get; set; }

	public int PlayerId { get; set; }

	[Range(1, 255)]
	public int Quantity { get; set; }

	[ForeignKey(nameof(LootId))]
	public virtual Loot Loot { get; set; } = null!;

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}
public class Loot
{
	public Loot(CreateLoot dto)
	{
		Name = dto.Name;
		Quantity = dto.Quantity;
	}

	[Key]
	public int Id { get; set; }

	[Range(0, 255)]
	public int Quantity { get; set; }

	public string Name { get; set; }

	[InverseProperty(nameof(LootRequest.Loot))]
	public virtual ICollection<LootRequest> LootRequests { get; } = null!;
}
public class CreateLootRequest
{
	[StringLength(24)]
	public string CharacterName { get; set; } = null!;

	public bool IsMain { get; set; }
	public EQClass Class { get; set; } // Enum.IsDefined
	public int LootId { get; set; }

	[Range(1, 255)]
	public int Quantity { get; set; }
}
public class CreateLoot
{
	public int Quantity { get; set; }
	public string Name { get; set; } = null!;
}
public enum EQClass
{
	Bard = 0,
	Beastlord = 1,
	Beserker = 2,
	Cleric = 3,
	Druid = 4,
	Enchanter = 5,
	Magician = 6,
	Monk = 7,
	Necromancer = 8,
	Paladin = 9,
	Ranger = 10,
	Rogue = 11,
	Shadowknight = 12,
	Shaman = 13,
	Warrior = 14,
	Wizard = 15,
}