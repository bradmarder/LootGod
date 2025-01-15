using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public enum Expansion : byte
{
	CoV = 0,
	ToL = 1,
	NoS = 3,
	LS = 4,
	ToB = 5,
}

[Index(nameof(Expansion))]
[Index(nameof(Name), IsUnique = true)]
public class Item
{
	private Item() { }
	public Item(string name, Expansion expansion)
	{
		Name = name;
		Expansion = expansion;
	}

	[Key]
	public int Id { get; set; }

	public Expansion Expansion { get; set; }

	public long CreatedDate { get; set; }

	[Required]
	[StringLength(255)]
	public string Name { get; set; } = null!;

	[InverseProperty(nameof(LootRequest.Item))]
	public virtual List<LootRequest> LootRequests { get; } = new();

	[InverseProperty(nameof(Loot.Item))]
	public virtual List<Loot> Loots { get; } = new();
}
