namespace LootGod;

public class LootRequestDto
{
	public LootRequestDto(LootRequest model)
	{
		Id = model.Id;
		CreatedDate = model.CreatedDate;
		IP = model.IP;
		MainName = model.MainName;
		CharacterName = model.CharacterName;
		Class = model.Class;
		Spell = model.Spell;
		LootId = model.LootId;
		Quantity = model.Quantity;
		IsAlt = model.IsAlt;
		Granted = model.Granted;
		CurrentItem = model.CurrentItem;
	}

	public int Id { get; }
	public DateTime CreatedDate { get; }
	public string? IP { get; }
	public string MainName { get; }
	public string CharacterName { get; }
	public string? Spell { get; }
	public EQClass Class { get; }
	public int LootId { get; }
	public int Quantity { get; }
	public bool IsAlt { get; }
	public bool Granted { get; }
	public string CurrentItem { get; }
}
