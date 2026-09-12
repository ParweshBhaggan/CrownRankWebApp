using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrownRank.Infrastructure.Persistence;

public sealed class PendingRankingEntryRepository(CrownRankDbContext dbContext)
    : IPendingRankingEntryRepository
{
    public Task<PendingRankingEntry?> GetByReferenceAsync(Guid referenceId, CancellationToken cancellationToken) =>
        dbContext.PendingRankingEntries.Include(x => x.SocialProfiles)
            .SingleOrDefaultAsync(x => x.ReferenceId == referenceId, cancellationToken);

    public Task<PendingRankingEntry?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.PendingRankingEntries.Include(x => x.SocialProfiles)
            .SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

    public Task AddAsync(PendingRankingEntry entry, CancellationToken cancellationToken) =>
        dbContext.PendingRankingEntries.AddAsync(entry, cancellationToken).AsTask();

    public void Remove(PendingRankingEntry entry) => dbContext.PendingRankingEntries.Remove(entry);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException(
                "A checkout already exists for this username or request reference.", exception);
        }
    }
}
