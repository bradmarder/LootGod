using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LootGodContext>(x => x.UseSqlite(""));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

public class LootGodContext : DbContext
{
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
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

	public int ItemId { get; set; }

	[ForeignKey(nameof(ItemId))]
	public virtual Item Item { get; set; } = null!;
}

public class Item
{
	public Item(string name)
	{
		Name = name;
	}

	[Key]
	public int Id { get; set; }

	public string Name { get; set; }

	[InverseProperty(nameof(LootRequest.Item))]
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