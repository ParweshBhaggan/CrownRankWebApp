using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data.Seeders;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<PaymentOperation> PaymentOperations
        {
            get;
            set;
        }

        public DbSet<ScoreAddition> ScoreAdditions
        {
            get;
            set;
        }

        public DbSet<Entry> Entries
        {
            get;
            set;
        }

        public DbSet<Category> Categories
        {
            get;
            set;
        }

        public DbSet<SocialMediaDefault> SocialMediaDefaults
        {
            get;
            set;
        }

        public DbSet<SocialMediaPlatform> SocialMediaPlatforms
        {
            get;
            set;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Entry>()
                .HasMany(e => e.Categories)
                .WithMany(c => c.Entries)
                .UsingEntity(j => j.ToTable("EntryCategories"));

            modelBuilder.Entity<Entry>()
                .HasMany(e => e.SocialMediaPlatforms)
                .WithOne(s => s.Entry)
                .HasForeignKey(s => s.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialMediaPlatform>()
                .HasOne(s => s.Platform)
                .WithMany()
                .HasForeignKey(s => s.PlatformId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Entry>()
                .Property(e => e.Score)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Entry>()
                .HasIndex(x => x.Username)
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(x => x.Name)
                .IsUnique();

            modelBuilder.Entity<SocialMediaDefault>()
                .HasIndex(x => x.Name)
                .IsUnique();

            modelBuilder.Entity<ScoreAddition>()
                .HasOne(addition => addition.Entry).WithMany()
                .HasForeignKey(addition => addition.EntryId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ScoreAddition>().Property(addition => addition.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<ScoreAddition>().HasIndex(addition => new
                {
                    addition.CreatedDate,
                    addition.EntryId
                });

            modelBuilder.Entity<PaymentOperation>().Property(payment => payment.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<PaymentOperation>().HasIndex(payment => payment.ReservedUsername).IsUnique();
            modelBuilder.Entity<PaymentOperation>().HasIndex(payment => payment.SessionId).IsUnique();
            modelBuilder.Entity<PaymentOperation>().HasIndex(payment => payment.PaymentIntentId).IsUnique();
            modelBuilder.Entity<PaymentOperation>().HasIndex(payment => new
                {
                    payment.FulfilledAt,
                    payment.Status,
                    payment.CreatedDate
                });

            CategorySeeder.Seed(modelBuilder);
            SocialMediaSeeder.Seed(modelBuilder);
        }
    }
}
