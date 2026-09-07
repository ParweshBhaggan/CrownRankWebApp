using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrownRank.Infrastructure.Persistence.Configurations;

internal sealed class SocialProfileConfiguration : IEntityTypeConfiguration<SocialProfile>
{
    public void Configure(EntityTypeBuilder<SocialProfile> builder)
    {
        builder.ToTable("social_profiles"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.CreatorId, x.Platform }).IsUnique();
    }
}
