using Microsoft.EntityFrameworkCore;
using WbGenerator.Enitites;

namespace WbGenerator.Persistence;

/// <summary>
/// Database context for the room booking system configured for SQLite.
/// </summary>
/// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdvancementTierOverride> AdvancementTierOverrides => Set<AdvancementTierOverride>();
    public DbSet<IndividualBlockReward> IndividualBlockRewards => Set<IndividualBlockReward>();
    public DbSet<TierCustomBlockReward> TierCustomBlockRewards => Set<TierCustomBlockReward>();

    /// <summary>
    /// Configures the model and entity mappings for SQLite.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}