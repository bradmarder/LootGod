using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	public class CreateLootRequest
	{
		[StringLength(24)]
		public string MainName { get; set; } = null!;

		[StringLength(24)]
		public string CharacterName { get; set; } = null!;

		//public bool IsMain { get; set; }
		public EQClass Class { get; set; } // Enum.IsDefined
		public int LootId { get; set; }

		[Range(1, 255)]
		public int Quantity { get; set; }
	}
}
