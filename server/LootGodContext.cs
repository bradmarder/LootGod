using Microsoft.EntityFrameworkCore;

public class LootGodContext : DbContext
{
	public LootGodContext(DbContextOptions<LootGodContext> options) : base(options)
	{
		ChangeTracker.LazyLoadingEnabled = false;
	}

	public DbSet<Guild> Guilds => Set<Guild>();
	public DbSet<LootRequest> LootRequests => Set<LootRequest>();
	public DbSet<Item> Items => Set<Item>();
	public DbSet<Loot> Loots => Set<Loot>();
	public DbSet<Player> Players => Set<Player>();
	public DbSet<RaidDump> RaidDumps => Set<RaidDump>();
	public DbSet<Rank> Ranks => Set<Rank>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// https://github.com/dotnet/efcore/issues/10228
		//modelBuilder
		//	.Entity<Item>()
		//	.Property(x => x.Id)
		//	.UseAutoincrement(false);

		modelBuilder.Entity<Loot>().ToTable(x =>
		{
			x.HasCheckConstraint("CK_Loot_Quantity", $"{nameof(Loot.RaidQuantity)} > {0} OR {nameof(Loot.RotQuantity)} > {0}");
		});

		const string unixEpoch = "1000 * unixepoch()";

		modelBuilder
			.Entity<Item>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql(unixEpoch);

		modelBuilder
			.Entity<Guild>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql(unixEpoch);

		modelBuilder
			.Entity<LootRequest>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql(unixEpoch);

		modelBuilder
			.Entity<Player>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql(unixEpoch);

		modelBuilder
			.Entity<Rank>()
			.Property(x => x.CreatedDate)
			.HasDefaultValueSql(unixEpoch);
	}
}
