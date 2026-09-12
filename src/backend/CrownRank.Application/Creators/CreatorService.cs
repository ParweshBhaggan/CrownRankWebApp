using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class CreatorService(
    ICreatorRepository repository,
    IPendingRankingEntryRepository pendingEntries,
    IProfileImageService images,
    IPaymentGateway payments,
    PaymentConfirmationService confirmations,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CreatorDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Where(IsVisible).Select(Map).OrderByDescending(x => x.TotalContributed).ThenBy(x => x.ScoreReachedAt).ThenBy(x => x.Id).ToList();

    public async Task<IReadOnlyList<CreatorDto>> GetDailyAsync(DateOnly date, CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Where(IsVisible).Select(creator => MapForDate(creator, date))
            .Where(x => x.TotalContributed > 0).OrderByDescending(x => x.TotalContributed).ThenBy(x => x.ScoreReachedAt).ThenBy(x => x.Id).ToList();

    public async Task<CreatorDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await repository.GetByIdAsync(id, cancellationToken);
        return creator is null || !IsVisible(creator) ? null : Map(creator);
    }

    public async Task<EntryCheckoutResult> StartEntryCheckoutAsync(CreateCreatorCommand command, CancellationToken cancellationToken)
    {
        Money.Validate(command.InitialAmount);
        if (command.EntryReference == Guid.Empty) throw new ArgumentException("An entry reference is required.");
        var username = command.Username.Trim().TrimStart('@').ToLowerInvariant();
        var completed = await repository.GetByEntryReferenceAsync(command.EntryReference, cancellationToken);
        if (completed is not null && completed.Contributions.Count > 0)
        {
            if (!Matches(completed, command, username))
                throw new InvalidOperationException("This entry reference was already used for different details.");
            return new(completed.Id, new CheckoutSession(completed.Contributions.First().PaymentReference, true));
        }

        var legacyPendingCreator = completed ?? await repository.GetByUsernameAsync(username, cancellationToken);
        if (legacyPendingCreator is not null)
        {
            if (legacyPendingCreator.Contributions.Count > 0)
                throw new InvalidOperationException("That username is already ranked.");
            repository.Remove(legacyPendingCreator);
            await repository.SaveChangesAsync(cancellationToken);
            if (legacyPendingCreator.ImageStorageKey is not null)
                await images.DeleteAsync(legacyPendingCreator.ImageStorageKey, CancellationToken.None);
        }

        var pending = await pendingEntries.GetByReferenceAsync(command.EntryReference, cancellationToken)
            ?? await pendingEntries.GetByUsernameAsync(username, cancellationToken);
        if (pending is not null)
        {
            if (!pending.Matches(command.Name, username, command.Category, command.InitialAmount,
                    command.SocialProfiles.Select(x => (x.Platform, x.Url)).ToList()))
                throw new InvalidOperationException("Retry checkout using the original profile details and amount.");
            return new(pending.CreatorId, await StartOpeningCheckoutAsync(pending, cancellationToken));
        }

        if (command.SocialProfiles.Count is < 1 or > 5) throw new ArgumentException("Provide between one and five social profiles.");
        var stored = command.Image is null ? null : await images.SaveAsync(new(command.Image, command.ImageFileName!, command.ImageContentType!, command.ImageLength), cancellationToken);
        PendingRankingEntry entry;
        try
        {
            entry = new PendingRankingEntry(
                command.EntryReference,
                Guid.NewGuid(),
                command.Name,
                username,
                command.Category,
                command.InitialAmount,
                stored?.Url ?? "/assets/default-profile.svg",
                stored?.StorageKey,
                timeProvider.GetUtcNow(),
                command.SocialProfiles.Select(x => (x.Platform, x.Url)).ToList());
            await pendingEntries.AddAsync(entry, cancellationToken);
            await pendingEntries.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (stored is not null) await images.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        return new(entry.CreatorId, await StartOpeningCheckoutAsync(entry, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await repository.GetByIdAsync(id, cancellationToken);
        if (creator is null) return false;
        creator.Hide();
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CheckoutSession> StartOpeningCheckoutAsync(PendingRankingEntry entry, CancellationToken cancellationToken)
    {
        var checkout = new CheckoutRequest(entry.CreatorId, entry.Amount, "USD", "ranking-entry", entry.ReferenceId);
        var session = await payments.CreateCheckoutAsync(checkout, cancellationToken);
        if (session.Confirmed)
        {
            await confirmations.ConfirmAsync(new PaymentConfirmation(
                entry.CreatorId, entry.Amount, "USD", "ranking-entry", entry.ReferenceId, session.Id), cancellationToken);
        }
        return session;
    }

    private static bool Matches(Creator creator, CreateCreatorCommand command, string username) =>
        creator.Name == command.Name.Trim() && creator.Username == username &&
        creator.Category == command.Category && creator.OpeningAmount == command.InitialAmount;

    private static bool IsVisible(Creator creator) => !creator.IsHidden && creator.Contributions.Count > 0;

    private CreatorDto Map(Creator creator)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return new(creator.Id, creator.Username, creator.Name,
            ToKebabCase(creator.Category), creator.ImageUrl,
            creator.SocialProfiles.Select(x => new SocialProfileDto(x.Id, ToKebabCase(x.Platform), x.Url)).ToList(),
            creator.Contributions.Sum(x => x.Amount), creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == today).Sum(x => x.Amount), creator.CreatedAt, creator.Contributions.Select(x => x.ConfirmedAt).DefaultIfEmpty(creator.CreatedAt).Max());
    }

    private CreatorDto MapForDate(Creator creator, DateOnly date)
    {
        var score = creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == date).Sum(x => x.Amount);
        var mapped = Map(creator);
        return mapped with { TotalContributed = score, DailyContributed = score, ScoreReachedAt = creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == date).Select(x => x.ConfirmedAt).DefaultIfEmpty(creator.CreatedAt).Max() };
    }

    private static string ToKebabCase<T>(T value) where T : Enum => value.ToString() switch
    {
        "AdultEntertainment" => "adult-entertainment", "BeautyFashion" => "beauty-fashion",
        "FitnessWellness" => "fitness-wellness", "ArtDesign" => "art-design", _ => value.ToString().ToLowerInvariant()
    };
}
