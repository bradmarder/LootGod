using LootGod;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;

namespace LootGod;

public class OldContext : DbContext
{
	public OldContext(DbContextOptions<OldContext> options) : base(options)
	{
		ChangeTracker.LazyLoadingEnabled = false;
	}

	public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Loot> Loots => Set<Loot>();
	public DbSet<LootLock> LootLocks => Set<LootLock>();

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
	}
}