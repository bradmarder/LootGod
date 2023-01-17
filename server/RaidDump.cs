using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[PrimaryKey(nameof(Timestamp), nameof(PlayerId))]
public class RaidDump
{
	public RaidDump() { }
	public RaidDump(long timestamp, int playerId)
	{
		Timestamp = timestamp;
		PlayerId = playerId;
	}

	public long Timestamp { get; set; }

	public int PlayerId { get; set; }

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}

