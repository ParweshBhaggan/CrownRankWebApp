using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.Persistence;

public sealed class CrownRankDbContext(DbContextOptions<CrownRankDbContext> options)
    : DbContext(options)
{
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<SocialProfile> SocialProfiles => Set<SocialProfile>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<PendingRankingEntry> PendingRankingEntries => Set<PendingRankingEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("CrownrankSchema");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrownRankDbContext).Assembly);
    }
}
