using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootGodContext : DbContext
{
	public LootGodContext(DbContextOptions<LootGodContext> options) : base(options)
	{
		ChangeTracker.LazyLoadingEnabled = false;
	}

	public DbSet<Guild> Guilds => Set<Guild>();
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Loot> Loots => Set<Loot>();
	public DbSet<Player> Players => Set<Player>();
	public DbSet<RaidDump> RaidDumps => Set<RaidDump>();
	public DbSet<Rank> Ranks => Set<Rank>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder
			.Entity<Guild>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<LootRequest>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<Player>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<Rank>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");
	}
}
