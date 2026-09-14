using CrownRank.Application.Abstractions;
using CrownRank.Application.Services;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using CrownRank.Infrastructure.Payments;
using CrownRank.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.IntegrationTest;

public sealed class PersistenceTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Paid_entry_and_boost_survive_new_context_and_rank_by_confirmed_contributions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CrownRankDbContext>().UseSqlite(connection).Options;
        Guid entryId;
        Guid attemptId;
        var gateway = new MockPaymentGateway();
        await using (var db = new CrownRankDbContext(options))
        {
            await db.Database.MigrateAsync();
            var category = Category.Create("Creators", null, Day);
            var entry = CreateEntry(category);
            db.Categories.Add(category);
            db.Entries.Add(entry);
            await db.SaveChangesAsync();
            var payments = Service(db, gateway);
            var started = await payments.StartAsync(entry.Id, Money.Create(500, "EUR"), CrownRank.Application.Payments.PaymentPurpose.InitialEntry);
            var first = await payments.ConfirmAsync(started.AttemptId);
            var repeated = await payments.ConfirmAsync(started.AttemptId);
            Assert.Equal(first?.Id, repeated?.Id);
            entryId = entry.Id;
            attemptId = started.AttemptId;
        }
        await using (var db = new CrownRankDbContext(options))
        {
            Assert.Equal(EntryStatus.Published, (await db.Entries.FindAsync(entryId))!.Status);
            Assert.Single(await db.Contributions.ToListAsync());
            var payments = Service(db, gateway);
            var boost = await payments.StartBoostAsync(entryId, Money.Create(200, "EUR"));
            Assert.NotNull(await payments.ConfirmAsync(boost.AttemptId));
            var queries = new EfLeaderboardQueries(db);
            Assert.Equal(700, (await queries.GetGlobalAsync(null)).Single().ScoreInMinorUnits);
            Assert.Equal(700, (await queries.GetDailyAsync(DateOnly.FromDateTime(Day.UtcDateTime), null)).Single().ScoreInMinorUnits);
            Assert.Equal(2, await db.Contributions.CountAsync());
        }
    }

    [Fact]
    public async Task Duplicate_provider_reference_is_rejected_by_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CrownRankDbContext>().UseSqlite(connection).Options;
        await using var db = new CrownRankDbContext(options);
        await db.Database.MigrateAsync();
        var category = Category.Create("Creators", null, Day);
        var entry = CreateEntry(category);
        db.Categories.Add(category);
        db.Entries.Add(entry);
        var a = RankingContribution.CreateInitialPayment(entry, Money.Create(100, "EUR"), "mock", "duplicate", Day);
        var b = RankingContribution.CreateInitialPayment(entry, Money.Create(100, "EUR"), "mock", "duplicate", Day);
        db.Contributions.AddRange(a, b);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Hidden_entries_are_absent_from_public_rankings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CrownRankDbContext>().UseSqlite(connection).Options;
        await using var db = new CrownRankDbContext(options);
        await db.Database.MigrateAsync();
        var category = Category.Create("Creators", null, Day);
        var entry = CreateEntry(category);
        var contribution = RankingContribution.CreateInitialPayment(entry, Money.Create(100, "EUR"), "mock", "reference", Day);
        entry.Publish(contribution);
        entry.Hide(Day.AddMinutes(1));
        db.Categories.Add(category);
        db.Entries.Add(entry);
        db.Contributions.Add(contribution);
        await db.SaveChangesAsync();
        Assert.Empty(await new EfLeaderboardQueries(db).GetGlobalAsync(null));
    }

    private static Entry CreateEntry(Category category) => Entry.Create("Creator", "creator", category, "image.png",
        AgreementAcceptance.Create("v1", "v1", "v1", Day),
        [SocialMediaLink.Create(SocialMediaPlatform.Instagram, "https://instagram.com/creator")], Day);

    private static PaymentService Service(CrownRankDbContext db, MockPaymentGateway gateway) => new(
        new EntryRepository(db), new ContributionRepository(db), new PaymentAttemptRepository(db),
        gateway, new EfUnitOfWork(db), new TestClock());

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Day; }
}
