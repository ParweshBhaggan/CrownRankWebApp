using CrownRank.Application.Payments;
using CrownRank.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CrownRank.Infrastructure.Persistence;

public sealed class CrownRankDbContext(DbContextOptions<CrownRankDbContext> options) : DbContext(options)
{
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RankingContribution> Contributions => Set<RankingContribution>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.CreatedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.UpdatedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.ArchivedAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.HasIndex(x => x.Status);
        });

        model.Entity<Entry>(b =>
        {
            b.ToTable("Entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Username).IsRequired().HasMaxLength(100);
            b.Property(x => x.ProfileImageKey).IsRequired().HasMaxLength(500);
            b.Property(x => x.Status).HasConversion<int>().IsConcurrencyToken();
            b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.OwnsOne(x => x.AgreementAcceptance, a =>
            {
                a.Property(x => x.TermsVersion).IsRequired().HasMaxLength(50);
                a.Property(x => x.PrivacyPolicyVersion).IsRequired().HasMaxLength(50);
                a.Property(x => x.RulesVersion).IsRequired().HasMaxLength(50);
                a.Property(x => x.AcceptedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            });
            b.OwnsMany(x => x.SocialMediaLinks, s =>
            {
                s.ToTable("SocialMediaLinks");
                s.WithOwner().HasForeignKey("EntryId");
                s.HasKey(x => x.Id);
                s.Property(x => x.Id).ValueGeneratedNever();
                s.Property(x => x.Platform).HasConversion<int>();
                s.Property(x => x.Url).IsRequired().HasMaxLength(2048);
                s.Property(x => x.CustomPlatformName).HasMaxLength(50);
            });
            b.Navigation(x => x.SocialMediaLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.CreatedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.UpdatedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.PublishedAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.Property(x => x.HiddenAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.Property(x => x.ArchivedAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.HasIndex(x => new { x.CategoryId, x.Status });
        });

        model.Entity<RankingContribution>(b =>
        {
            b.ToTable("RankingContributions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne<Entry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.ExclusionReason).HasConversion<int?>();
            b.Property(x => x.PaymentProvider).IsRequired().HasMaxLength(50);
            b.Property(x => x.PaymentReference).IsRequired().HasMaxLength(255);
            b.OwnsOne(x => x.Amount, a =>
            {
                a.Property(x => x.AmountInMinorUnits).IsRequired();
                a.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            });
            b.Property(x => x.ConfirmedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.ExcludedAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.HasIndex(x => new { x.PaymentProvider, x.PaymentReference }).IsUnique();
            b.HasIndex(x => new { x.EntryId, x.Status, x.ConfirmedAtUtc });
        });

        model.Entity<PaymentAttempt>(b =>
        {
            b.ToTable("PaymentAttempts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne<Entry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Purpose).HasConversion<int>();
            b.Property(x => x.State).HasConversion<int>().IsConcurrencyToken();
            b.Property(x => x.Provider).HasMaxLength(50);
            b.Property(x => x.Reference).HasMaxLength(255);
            b.Property(x => x.CheckoutUrl).HasMaxLength(2048);
            b.OwnsOne(x => x.ExpectedAmount, a =>
            {
                a.Property(x => x.AmountInMinorUnits).IsRequired();
                a.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            });
            b.Property(x => x.CreatedAtUtc).HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
            b.Property(x => x.ConfirmedAtUtc).HasConversion(x => x.HasValue ? x.Value.UtcTicks : (long?)null,
                x => x.HasValue ? new DateTimeOffset(x.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
            b.HasIndex(x => new { x.Provider, x.Reference }).IsUnique();
            b.HasIndex(x => new { x.EntryId, x.State });
        });
    }
}
