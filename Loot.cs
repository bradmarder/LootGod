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
		private static readonly HashSet<string> _spellPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Minor",
			"Lesser",
			"Median",
			"Greater",
			"Glowing",
		};

		private static readonly HashSet<string> _spellSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Rune",
		};

		public Loot() { }
		public Loot(CreateLoot dto)
		{
			Name = dto.Name;
			Quantity = dto.Quantity;
		}

		[Key]
		public int Id { get; set; }

		public byte Quantity { get; set; }

		[Required]
		[MaxLength(255)]
		public string Name { get; set; } = null!;

		[InverseProperty(nameof(LootRequest.Loot))]
		public virtual ICollection<LootRequest> LootRequests { get; } = null!;

		public virtual bool IsSpell =>
			_spellPrefixes.Any(x => Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
			&& _spellSuffixes.Any(x => Name.EndsWith(x, StringComparison.OrdinalIgnoreCase));
	}
}
