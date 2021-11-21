using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class LootGodContext : DbContext
{
	public LootGodContext(DbContextOptions<LootGodContext> options) : base(options)
	{
		ChangeTracker.LazyLoadingEnabled = false;
	}

	public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Loot> Loots => Set<Loot>();
	public DbSet<LootLock> LootLocks => Set<LootLock>();
	//public DbSet<Player> Players => Set<Player>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder
			.Entity<LootRequest>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<LoginAttempt>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql("CURRENT_TIMESTAMP");

		modelBuilder
			.Entity<LootLock>()
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
