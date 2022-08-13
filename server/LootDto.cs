namespace LootGod;

public class LootDto
{
	public LootDto(Loot model)
	{
		Id = model.Id;
		Quantity = model.Quantity;
		Name = model.Name;
		IsSpell = model.IsSpell;
	}

	public int Id { get; }
	public byte Quantity { get; }
	public string Name { get; }
	public bool IsSpell { get; }
}
