using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrownRank.Infrastructure.Persistence.Configurations;

internal sealed class PendingRankingEntryConfiguration : IEntityTypeConfiguration<PendingRankingEntry>
{
    public void Configure(EntityTypeBuilder<PendingRankingEntry> builder)
    {
        builder.ToTable("pending_ranking_entries");
        builder.HasKey(x => x.ReferenceId);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.CreatorId).IsUnique();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ImageStorageKey).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasMany(x => x.SocialProfiles).WithOne()
            .HasForeignKey(x => x.PendingRankingEntryReferenceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.SocialProfiles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PendingRankingEntrySocialProfileConfiguration
    : IEntityTypeConfiguration<PendingRankingEntrySocialProfile>
{
    public void Configure(EntityTypeBuilder<PendingRankingEntrySocialProfile> builder)
    {
        builder.ToTable("pending_ranking_entry_social_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.PendingRankingEntryReferenceId, x.Platform }).IsUnique();
    }
}
