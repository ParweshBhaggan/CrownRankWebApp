using CrownRank.Domain.Creators;

namespace CrownRank.Application.Abstractions;

public interface IPendingRankingEntryRepository
{
    Task<PendingRankingEntry?> GetByReferenceAsync(Guid referenceId, CancellationToken cancellationToken);
    Task<PendingRankingEntry?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task AddAsync(PendingRankingEntry entry, CancellationToken cancellationToken);
    void Remove(PendingRankingEntry entry);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
