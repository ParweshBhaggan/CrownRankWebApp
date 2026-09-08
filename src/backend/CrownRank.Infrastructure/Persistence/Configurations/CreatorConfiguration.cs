using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrownRank.Infrastructure.Persistence.Configurations;

internal sealed class CreatorConfiguration : IEntityTypeConfiguration<Creator>
{
    public void Configure(EntityTypeBuilder<Creator> builder)
    {
        builder.ToTable("creators");
        builder.HasKey(creator => creator.Id);
        builder.Property(creator => creator.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(creator => creator.LastName).HasMaxLength(80).IsRequired();
        builder.Property(creator => creator.Username).HasMaxLength(50).IsRequired();
        builder.HasIndex(creator => creator.Username).IsUnique();
        builder.Property(creator => creator.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(creator => creator.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(creator => creator.ImageStorageKey).HasMaxLength(200);
        builder.Property(x => x.OpeningAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.EntryReference).IsUnique();
        builder.Property(creator => creator.CreatedAt).IsRequired();
        builder.HasMany(creator => creator.SocialProfiles).WithOne().HasForeignKey(profile => profile.CreatorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(creator => creator.Contributions).WithOne().HasForeignKey(contribution => contribution.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(creator => creator.SocialProfiles).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(creator => creator.Contributions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

