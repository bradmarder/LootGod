using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	[Index(nameof(Name), IsUnique = true)]
	public class Loot
	{
		public Loot() { }
		public Loot(CreateLoot dto)
		{
			Name = dto.Name;
			Quantity = dto.Quantity;
		}

		[Key]
		public int Id { get; set; }

		public byte Quantity { get; set; }

		[MaxLength(255)]
		public string Name { get; set; } = null!;

		[InverseProperty(nameof(LootRequest.Loot))]
		public virtual ICollection<LootRequest> LootRequests { get; } = null!;
	}
}
