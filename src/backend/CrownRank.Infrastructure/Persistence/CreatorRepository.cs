using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.Persistence;

public sealed class CreatorRepository(CrownRankDbContext dbContext) : ICreatorRepository
{
    public async Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Creators.AsNoTracking().Include(x => x.SocialProfiles).Include(x => x.Contributions).ToListAsync(cancellationToken);
    public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Creators.Include(x => x.SocialProfiles).Include(x => x.Contributions).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) => dbContext.Creators.AnyAsync(x => x.Username == username, cancellationToken);
    public Task AddAsync(Creator creator, CancellationToken cancellationToken) => dbContext.Creators.AddAsync(creator, cancellationToken).AsTask();
    public void Remove(Creator creator) => dbContext.Creators.Remove(creator);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
