using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	//public class Player
	//{
	//	private static string GenerateRandomKey()
	//	{
	//		Span<byte> bytes = stackalloc byte[16];
	//		RandomNumberGenerator.Fill(bytes);
	//		return new Guid(bytes).ToString("n").Substring(0, 12);
	//	}

	//	public Player(CreatePlayer dto)
	//	{
	//		CharacterName = dto.Name;
	//		Key = GenerateRandomKey();
	//	}

	//	[Key]
	//	public int Id { get; set; }

	//	public DateTime CreatedDate { get; set; }

	//	[MaxLength(12)]
	//	public string Key { get; set; }

	//	[MaxLength(24)]
	//	public string CharacterName { get; set; }

	//	[InverseProperty(nameof(LootRequest.Player))]
	//	public virtual ICollection<LootRequest> LootRequests { get; } = null!;
	//}
}
