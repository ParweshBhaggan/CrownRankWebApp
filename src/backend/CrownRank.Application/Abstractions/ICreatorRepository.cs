using CrownRank.Domain.Creators;

namespace CrownRank.Application.Abstractions;

public interface ICreatorRepository
{
    Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken);
    Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task AddAsync(Creator creator, CancellationToken cancellationToken);
    void Remove(Creator creator);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
