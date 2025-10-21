public record LootDto
{
	public int ItemId { get; init; }
	public byte RaidQuantity { get; init; }
	public byte RotQuantity { get; init; }
	public ItemDto Item { get; init; } = null!;
}
