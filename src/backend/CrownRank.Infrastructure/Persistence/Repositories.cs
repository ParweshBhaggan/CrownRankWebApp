using CrownRank.Application.Abstractions;
using CrownRank.Application.Payments;
using CrownRank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.Persistence;

public sealed class EntryRepository(CrownRankDbContext db) : IEntryRepository
{
    public Task<Entry?> GetAsync(Guid id, CancellationToken ct = default) => db.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(Entry entry, CancellationToken ct = default) => await db.Entries.AddAsync(entry, ct);
}

public sealed class CategoryRepository(CrownRankDbContext db) : ICategoryRepository
{
    public Task<Category?> GetAsync(Guid id, CancellationToken ct = default) => db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken ct = default)
        => await db.Categories.AsNoTracking().Where(x => x.Status == CategoryStatus.Active).OrderBy(x => x.Name).ToListAsync(ct);
    public async Task AddAsync(Category category, CancellationToken ct = default) => await db.Categories.AddAsync(category, ct);
}

public sealed class ContributionRepository(CrownRankDbContext db) : IContributionRepository
{
    public Task<RankingContribution?> FindByPaymentAsync(string provider, string reference, CancellationToken ct = default)
        => db.Contributions.FirstOrDefaultAsync(x => x.PaymentProvider == provider.ToLowerInvariant() && x.PaymentReference == reference, ct);
    public async Task AddAsync(RankingContribution contribution, CancellationToken ct = default)
        => await db.Contributions.AddAsync(contribution, ct);
}

public sealed class PaymentAttemptRepository(CrownRankDbContext db) : IPaymentAttemptRepository
{
    public Task<PaymentAttempt?> GetAsync(Guid id, CancellationToken ct = default)
        => db.PaymentAttempts.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(PaymentAttempt attempt, CancellationToken ct = default)
        => await db.PaymentAttempts.AddAsync(attempt, ct);
}

public sealed class EfUnitOfWork(CrownRankDbContext db) : IUnitOfWork
{
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await action(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }
}
