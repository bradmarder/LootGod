using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	[Index(nameof(IP))]
	[Index(nameof(CharacterName), nameof(LootId), IsUnique = true)]
	public class LootRequest
	{
		public LootRequest(CreateLootRequest dto, string? ip)
		{
			IP = ip;
			MainName = dto.MainName;
			CharacterName = dto.CharacterName;
			//IsMain = dto.IsMain;
			Class = dto.Class;
			LootId = dto.LootId;
			Quantity = dto.Quantity;
		}

		[Key]
		public int Id { get; set; }

		public DateTime CreatedDate { get; set; }

		public string? IP { get; set; }

		[MaxLength(24)]
		public string MainName { get; set; }

		[MaxLength(24)]
		public string CharacterName { get; set; }

		//public bool IsMain { get; set; }

		public EQClass Class { get; set; }

		public int LootId { get; set; }

		//public int PlayerId { get; set; }

		[Range(1, 255)]
		public int Quantity { get; set; }

		[ForeignKey(nameof(LootId))]
		public virtual Loot Loot { get; set; } = null!;

		//[ForeignKey(nameof(PlayerId))]
		//public virtual Player Player { get; set; } = null!;
	}
}
