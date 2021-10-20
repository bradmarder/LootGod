using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace LootGod
{
	[Index(nameof(IP))]
	[Index(nameof(CreatedDate))]
	[Index(nameof(CharacterName), nameof(LootId), nameof(Spell), IsUnique = true)]
	public class LootRequest
	{
		public LootRequest() { }
		public LootRequest(CreateLootRequest dto, string? ip)
		{
			IP = ip;
			MainName = dto.MainName.Trim();
			CharacterName = dto.CharacterName.Trim();
			Spell = dto.Spell;
			Class = dto.Class;
			LootId = dto.LootId;
			Quantity = dto.Quantity;
		}

		[Key]
		public int Id { get; set; }

		public DateTime CreatedDate { get; set; }

		public string? IP { get; set; }

		[Required]
		[MaxLength(24)]
		public string MainName { get; set; } = null!;

		[Required]
		[MaxLength(24)]
		public string CharacterName { get; set; } = null!;

		/// <summary>
		/// Required only if loot type is a spell
		/// </summary>
		[MaxLength(255)]
		public string? Spell { get; set; }

		//public bool IsMain { get; set; }

		public EQClass Class { get; set; }

		public int LootId { get; set; }

		public bool Granted { get; set; }

		//public int PlayerId { get; set; }+

		[Range(1, 255)]
		public byte Quantity { get; set; }

		[ForeignKey(nameof(LootId))]
		public virtual Loot Loot { get; set; } = null!;

		public virtual bool IsAlt => !string.Equals(MainName, CharacterName, StringComparison.OrdinalIgnoreCase); 

		//[ForeignKey(nameof(PlayerId))]
		//public virtual Player Player { get; set; } = null!;
	}
}
