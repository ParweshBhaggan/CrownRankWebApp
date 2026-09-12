using CrownRank.Api.Contracts;
using CrownRank.Api.Data;
using CrownRank.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace CrownRank.Api.Domain;

public sealed class CreatorService(
    CrownRankDbContext dbContext,
    IProfileImageStore images,
    TimeProvider timeProvider,
    ILogger<CreatorService> logger)
{
    public async Task<IReadOnlyList<CreatorDto>> GetAllAsync(DateOnly? date, CancellationToken cancellationToken)
    {
        var creators = await dbContext.Creators.AsNoTracking().AsSplitQuery()
            .Where(x => !x.IsDeleted)
            .Include(x => x.SocialProfiles)
            .Include(x => x.Contributions)
            .ToListAsync(cancellationToken);

        return creators.Select(x => Map(x, date))
            .Where(x => date is null || x.TotalContributed > 0)
            .OrderByDescending(x => x.TotalContributed)
            .ThenBy(x => x.ScoreReachedAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public async Task<CreatorDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await CreatorQuery(tracking: false)
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        return creator is null ? null : Map(creator, null);
    }

    public async Task<CreatorDto> CreateAsync(CreateCreatorCommand command, CancellationToken cancellationToken)
    {
        if (command.EntryReference == Guid.Empty) BadRequest("An entry reference is required.");
        var name = InputRules.Name(command.Name);
        var username = InputRules.Username(command.Username);
        var category = InputRules.Category(command.Category);
        var amount = InputRules.Money(command.InitialAmount);
        var creatorId = Guid.NewGuid();
        var socialProfiles = InputRules.SocialProfiles(creatorId, command.SocialProfiles);

        var existing = await CreatorQuery(tracking: false)
            .SingleOrDefaultAsync(x => x.EntryReference == command.EntryReference, cancellationToken);
        if (existing is not null)
        {
            if (!EntryMatches(existing, name, username, category, amount, socialProfiles))
                Conflict("This entry reference was already used for different details.");
            return Map(existing, null);
        }

        if (await dbContext.Creators.AnyAsync(x => x.Username == username, cancellationToken))
            Conflict("That username already exists.");

        StoredImage? storedImage = null;
        try
        {
            storedImage = command.Image is null ? null : await images.SaveAsync(command.Image, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var creator = new Creator(creatorId, command.EntryReference, name, username, category,
                storedImage?.Url ?? "/assets/default-profile.svg", now);
            creator.SocialProfiles.AddRange(socialProfiles);
            creator.Contributions.Add(new Contribution(Guid.NewGuid(), creatorId, amount,
                ContributionKind.RankUp, now, $"entry:{command.EntryReference:N}"));
            dbContext.Creators.Add(creator);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(creator, null);
        }
        catch (DbUpdateException exception)
        {
            if (storedImage is not null) await images.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
            logger.LogWarning(exception, "A creator entry conflicted with another write");
            dbContext.ChangeTracker.Clear();
            var winner = await CreatorQuery(tracking: false)
                .SingleOrDefaultAsync(x => x.EntryReference == command.EntryReference, cancellationToken);
            if (winner is not null && EntryMatches(winner, name, username, category, amount, socialProfiles))
                return Map(winner, null);
            Conflict("That username or entry reference already exists.");
            throw;
        }
        catch
        {
            if (storedImage is not null) await images.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<CreatorDto> UpdateAsync(Guid id, UpdateCreatorRequest request,
        CancellationToken cancellationToken)
    {
        var name = InputRules.Name(request.Name);
        var username = InputRules.Username(request.Username);
        var category = InputRules.Category(request.Category);
        var profiles = InputRules.SocialProfiles(id, request.SocialProfiles);
        var creator = await CreatorQuery(tracking: true)
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw NotFound("Creator not found.");

        if (await dbContext.Creators.AnyAsync(x => x.Username == username && x.Id != id, cancellationToken))
            Conflict("That username already exists.");
        creator.Update(name, username, category, profiles);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(creator, null);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Creator {CreatorId} changed during update", id);
            Conflict("The creator changed during this request. Reload and retry.");
            throw;
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "A creator update conflicted with another write");
            Conflict("That username already exists.");
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await dbContext.Creators.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted,
            cancellationToken);
        if (creator is null) return false;
        creator.Delete();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Creator {CreatorId} changed during delete", id);
            Conflict("The creator changed during this request. Reload and retry.");
            throw;
        }
    }

    public async Task<CheckoutSession> BoostAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (request.ReferenceId == Guid.Empty || request.CreatorId == Guid.Empty)
            BadRequest("Checkout and creator references are required.");
        if (!string.Equals(request.Purpose, "creator-boost", StringComparison.Ordinal) ||
            !string.Equals(request.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            BadRequest("Only USD creator-boost checkouts are supported.");
        var amount = InputRules.Money(request.Amount);
        var paymentReference = $"mock:{request.ReferenceId:N}";

        var existing = await dbContext.Contributions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PaymentReference == paymentReference, cancellationToken);
        if (existing is not null)
        {
            if (existing.CreatorId != request.CreatorId || existing.Amount != amount ||
                existing.Kind != ContributionKind.Boost)
                Conflict("This checkout reference was already used for different details.");
            return new CheckoutSession(paymentReference, true);
        }

        var creator = await dbContext.Creators.SingleOrDefaultAsync(
            x => x.Id == request.CreatorId && !x.IsDeleted, cancellationToken)
            ?? throw NotFound("Creator not found.");
        dbContext.Contributions.Add(new Contribution(Guid.NewGuid(), creator.Id, amount,
            ContributionKind.Boost, timeProvider.GetUtcNow(), paymentReference));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CheckoutSession(paymentReference, true);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "A boost conflicted with another write");
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.Contributions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.PaymentReference == paymentReference, cancellationToken);
            if (winner is not null && winner.CreatorId == request.CreatorId && winner.Amount == amount &&
                winner.Kind == ContributionKind.Boost)
                return new CheckoutSession(paymentReference, true);
            Conflict("This checkout reference was already used for different details.");
            throw;
        }
    }

    private IQueryable<Creator> CreatorQuery(bool tracking)
    {
        var query = dbContext.Creators.AsSplitQuery().Include(x => x.SocialProfiles)
            .Include(x => x.Contributions).AsQueryable();
        return tracking ? query : query.AsNoTracking();
    }

    private CreatorDto Map(Creator creator, DateOnly? date)
    {
        var relevant = date is null
            ? creator.Contributions
            : creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == date).ToList();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var total = relevant.Sum(x => x.Amount);
        var daily = creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == today)
            .Sum(x => x.Amount);
        return new CreatorDto(creator.Id, creator.Username, creator.Name, InputRules.CategorySlug(creator.Category),
            creator.ImageUrl,
            creator.SocialProfiles.Select(x => new SocialProfileDto(x.Id, InputRules.PlatformSlug(x.Platform), x.Url))
                .OrderBy(x => x.Platform).ToList(),
            total, date is null ? daily : total,
            relevant.Select(x => x.ConfirmedAt).DefaultIfEmpty(creator.CreatedAt).Max(), creator.CreatedAt);
    }

    private static bool EntryMatches(Creator creator, string name, string username, CreatorCategory category,
        decimal amount, IReadOnlyCollection<SocialProfile> profiles) =>
        creator.Name == name && creator.Username == username && creator.Category == category &&
        creator.Contributions.Any(x => x.Kind == ContributionKind.RankUp && x.Amount == amount) &&
        creator.SocialProfiles.Count == profiles.Count && profiles.All(expected =>
            creator.SocialProfiles.Any(actual => actual.Platform == expected.Platform && actual.Url == expected.Url));

    private static ApiProblemException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Not found", detail);

    [DoesNotReturn]
    private static void BadRequest(string detail) =>
        throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid request", detail);

    [DoesNotReturn]
    private static void Conflict(string detail) =>
        throw new ApiProblemException(StatusCodes.Status409Conflict, "Conflict", detail);
}
