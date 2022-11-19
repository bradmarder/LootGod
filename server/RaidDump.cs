using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LootGod;

[Index(nameof(Timestamp))]
[Index(nameof(PlayerId), nameof(Timestamp), IsUnique = true)]
public class RaidDump
{
	public RaidDump() { }
	public RaidDump(DateTime timestamp, int playerId)
	{
		Timestamp = timestamp;
		PlayerId = playerId;
	}

	[Key]
	public int Id { get; set; }

	/// <summary>
	/// All dumps will be grouped by their timestamp.
	/// NOT UTC! This is the user's local datetime, which is most likely CST.
	/// </summary>
	public DateTime Timestamp { get; set; }

	public int PlayerId { get; set; }

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}

