using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite(""));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("CreateLoot", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLoot>();
	ArgumentNullException.ThrowIfNull(dto);
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	_ = db.Loots.Add(new(dto.Name, dto.Quantity));
	_ = await db.SaveChangesAsync();
});

app.MapPost("CreateLootRequest", async (context) =>
{
	var dto = await context.Request.ReadFromJsonAsync<CreateLootRequest>();
	ArgumentNullException.ThrowIfNull(dto);
	var db = context.RequestServices.GetRequiredService<LootGodContext>();
	_ = db.LootRequests.Add(new(dto.CharacterName));
	_ = await db.SaveChangesAsync();
});

app.Run();

public class LootGodContext : DbContext
{
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Loot> Loots => Set<Loot>();
}

public class LootRequest
{
	public LootRequest(string name)
	{
		CreatedDate = DateTime.UtcNow;
		CharacterName = name;
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	[MaxLength(24)]
	public string CharacterName { get; set; }

	public bool IsMain { get; set; }

	public EQClass Class { get; set; }

	public int LootId { get; set; }

	[Range(1, 255)]
	public int Quantity { get; set; }

	[ForeignKey(nameof(LootId))]
	public virtual Loot Loot { get; set; } = null!;
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
public class Loot
{
	public Loot(string name, int quantity)
	{
		Name = name;
		Quantity = quantity;
	}

	[Key]
	public int Id { get; set; }

	[Range(0, 255)]
	public int Quantity { get; set; }

	public string Name { get; set; }

	[InverseProperty(nameof(LootRequest.Loot))]
	public virtual ICollection<LootRequest> LootRequests { get; } = null!;
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