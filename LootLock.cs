using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace LootGod
{
	[Index(nameof(CreatedDate))]
	public class LootLock
	{
		public LootLock() { }
		public LootLock(bool @lock, string? ip)
		{
			IP = ip;
			Lock = @lock;
		}

		[Key]
		public int Id { get; set; }

		public DateTime CreatedDate { get; set; }

		public string? IP { get; set; }

		public bool Lock { get; set; }
	}
}
