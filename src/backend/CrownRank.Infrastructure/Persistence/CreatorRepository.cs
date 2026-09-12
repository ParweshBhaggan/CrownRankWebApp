using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrownRank.Infrastructure.Persistence;

public sealed class CreatorRepository(CrownRankDbContext dbContext) : ICreatorRepository
{
    public async Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Creators.AsNoTracking().AsSplitQuery()
            .Include(x => x.SocialProfiles).Include(x => x.Contributions)
            .ToListAsync(cancellationToken);
    public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Creators.Include(x => x.SocialProfiles).Include(x => x.Contributions).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Creator?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Creators.Include(x => x.SocialProfiles).Include(x => x.Contributions).SingleOrDefaultAsync(x => x.Username == username, cancellationToken);
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) => dbContext.Creators.AnyAsync(x => x.Username == username, cancellationToken);
    public Task AddAsync(Creator creator, CancellationToken cancellationToken) => dbContext.Creators.AddAsync(creator, cancellationToken).AsTask();
    public Task<Creator?> GetByEntryReferenceAsync(Guid reference, CancellationToken cancellationToken) =>
        dbContext.Creators.Include(x => x.SocialProfiles).Include(x => x.Contributions).SingleOrDefaultAsync(x => x.EntryReference == reference, cancellationToken);
    public Task<Contribution?> GetContributionAsync(string reference, CancellationToken cancellationToken) =>
        dbContext.Set<Contribution>().AsNoTracking().SingleOrDefaultAsync(x => x.PaymentReference == reference, cancellationToken);
    public void Remove(Creator creator) => dbContext.Creators.Remove(creator);
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException("This username or request reference already exists. Retry the original request if its response was interrupted.", ex);
        }
    }
}
