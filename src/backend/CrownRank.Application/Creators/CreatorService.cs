using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class CreatorService(ICreatorRepository repository, IProfileImageService images, IPaymentGateway payments, TimeProvider timeProvider)
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
        var previous = await repository.GetByEntryReferenceAsync(command.EntryReference, cancellationToken);
        var recoveredByUsername = false;
        if (previous is null)
        {
            previous = await repository.GetByUsernameAsync(username, cancellationToken);
            recoveredByUsername = previous is not null;
        }
        if (previous is not null)
        {
            if (recoveredByUsername && previous.Contributions.Count > 0)
                throw new InvalidOperationException("That username is already ranked.");
            if (previous.Name != command.Name.Trim() ||
                previous.Username != username ||
                previous.Category != command.Category || previous.OpeningAmount != command.InitialAmount)
                throw new InvalidOperationException("Retry an entry using its original details and amount.");
            if (previous.Contributions.Count == 0)
            {
                if (recoveredByUsername)
                {
                    previous.RecoverEntry(command.EntryReference);
                    await repository.SaveChangesAsync(cancellationToken);
                }
                return new(previous.Id, await StartOpeningCheckoutAsync(previous, command, cancellationToken));
            }
            return new(previous.Id, new CheckoutSession(previous.Contributions.First().PaymentReference, true));
        }
        if (await repository.UsernameExistsAsync(username, cancellationToken)) throw new InvalidOperationException("That username is already ranked.");
        if (command.SocialProfiles.Count is < 1 or > 5) throw new ArgumentException("Provide between one and five social profiles.");
        var stored = command.Image is null ? null : await images.SaveAsync(new(command.Image, command.ImageFileName!, command.ImageContentType!, command.ImageLength), cancellationToken);
        Creator creator;
        try
        {
            var now = timeProvider.GetUtcNow();
            creator = new Creator(Guid.NewGuid(), command.Name, username, command.Category, stored?.Url ?? "/assets/default-profile.svg", stored?.StorageKey, now);
            foreach (var social in command.SocialProfiles) creator.AddSocialProfile(social.Platform, social.Url);
            creator.PrepareEntry(command.EntryReference, command.InitialAmount);
            await repository.AddAsync(creator, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (stored is not null) await images.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        return new(creator.Id, await StartOpeningCheckoutAsync(creator, command, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await repository.GetByIdAsync(id, cancellationToken);
        if (creator is null) return false;
        creator.Hide();
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CheckoutSession> StartOpeningCheckoutAsync(Creator creator, CreateCreatorCommand command, CancellationToken cancellationToken)
    {
        var checkout = new CheckoutRequest(creator.Id, command.InitialAmount, "USD", "ranking-entry", command.EntryReference);
        var session = await payments.CreateCheckoutAsync(checkout, cancellationToken);
        if (session.Confirmed)
        {
            creator.AddContribution(command.InitialAmount, ContributionKind.RankUp, timeProvider.GetUtcNow(), session.Id);
            await repository.SaveChangesAsync(cancellationToken);
        }
        return session;
    }

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
