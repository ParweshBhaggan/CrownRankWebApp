using CrownRank.Domain.Creators;

namespace CrownRank.Application.Abstractions;

public interface ICreatorRepository
{
    Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken);
    Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Creator?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task AddAsync(Creator creator, CancellationToken cancellationToken);
    Task<Creator?> GetByEntryReferenceAsync(Guid reference, CancellationToken cancellationToken);
    Task<Contribution?> GetContributionAsync(string reference, CancellationToken cancellationToken);
    void Remove(Creator creator);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
