using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrownRank.Infrastructure.Persistence.Configurations;

internal sealed class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.ToTable("contributions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ConfirmedAt).IsRequired();
        builder.Property(x => x.PaymentReference).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.PaymentReference).IsUnique();
        builder.HasIndex(x => new { x.CreatorId, x.ConfirmedAt });
    }
}

