﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LootGod
{
	public class LootGodContext : DbContext
	{
		public LootGodContext(DbContextOptions<LootGodContext> options) : base(options) { }

		public DbSet<LootRequest> LootRequests => Set<LootRequest>();
		public DbSet<Loot> Loots => Set<Loot>();
		//public DbSet<Player> Players => Set<Player>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder
				.Entity<LootRequest>()
				.Property(x => x.CreatedDate)
				.HasDefaultValueSql("CURRENT_TIMESTAMP");

			//modelBuilder
			//	.Entity<Player>()
			//	.Property(x => x.CreatedDate)
			//	.HasDefaultValueSql("CURRENT_TIMESTAMP");

			//modelBuilder.Entity<Player>(entity =>
			//{
			//	entity.HasIndex(e => e.Key).IsUnique();
			//});
		}
	}
}