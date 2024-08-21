using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

[PrimaryKey(nameof(Timestamp), nameof(PlayerId))]
public class RaidDump
{
	public RaidDump(long timestamp, int playerId)
	{
		Timestamp = timestamp;
		PlayerId = playerId;
	}

	/// <summary>
	/// unix time seconds
	/// </summary>
	public long Timestamp { get; private set; }

	public int PlayerId { get; private set; }

	[ForeignKey(nameof(PlayerId))]
	public virtual Player Player { get; set; } = null!;
}
