using CrownRank.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Api.Data;

public sealed class CrownRankDbContext(DbContextOptions<CrownRankDbContext> options) : DbContext(options)
{
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<SocialProfile> SocialProfiles => Set<SocialProfile>();
    public DbSet<Contribution> Contributions => Set<Contribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CrownrankSchema");

        modelBuilder.Entity<Creator>(entity =>
        {
            entity.ToTable("creators");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.EntryReference).IsUnique();
            entity.HasMany(x => x.SocialProfiles).WithOne().HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Contributions).WithOne().HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SocialProfile>(entity =>
        {
            entity.ToTable("social_profiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Platform).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.CreatorId, x.Platform }).IsUnique();
        });

        modelBuilder.Entity<Contribution>(entity =>
        {
            entity.ToTable("contributions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.PaymentReference).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.PaymentReference).IsUnique();
            entity.HasIndex(x => new { x.CreatorId, x.ConfirmedAt });
        });
    }
}
