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
        builder.Property(creator => creator.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(creator => creator.Username).IsUnique();
    }
}

