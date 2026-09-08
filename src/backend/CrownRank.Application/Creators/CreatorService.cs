using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class CreatorService(ICreatorRepository repository, IProfileImageService images, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CreatorDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(Map).OrderByDescending(x => x.TotalContributedCents).ThenBy(x => x.JoinedAt).ToList();

    public async Task<IReadOnlyList<CreatorDto>> GetDailyAsync(DateOnly date, CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(creator => MapForDate(creator, date))
            .Where(x => x.TotalContributedCents > 0).OrderByDescending(x => x.TotalContributedCents).ThenBy(x => x.JoinedAt).ToList();

    public async Task<CreatorDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await repository.GetByIdAsync(id, cancellationToken);
        return creator is null ? null : Map(creator);
    }

    public async Task<CreatorDto> CreateAsync(CreateCreatorCommand command, CancellationToken cancellationToken)
    {
        var username = command.Username.Trim().TrimStart('@').ToLowerInvariant();
        if (await repository.UsernameExistsAsync(username, cancellationToken)) throw new InvalidOperationException("That username is already ranked.");
        if (command.SocialProfiles.Count is < 1 or > 5) throw new ArgumentException("Provide between one and five social profiles.");
        var stored = command.Image is null ? null : await images.SaveAsync(new(command.Image, command.ImageFileName!, command.ImageContentType!, command.ImageLength), cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var creator = new Creator(Guid.NewGuid(), command.FirstName, command.LastName, username, command.Category, stored?.Url ?? "/assets/default-profile.svg", stored?.StorageKey, command.Location, now);
            foreach (var social in command.SocialProfiles) creator.AddSocialProfile(social.Platform, social.Url);
            creator.AddContribution(command.InitialAmountCents, ContributionKind.RankUp, now, $"dev-confirmed-{Guid.NewGuid():N}");
            await repository.AddAsync(creator, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            return Map(creator);
        }
        catch
        {
            if (stored is not null) await images.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var creator = await repository.GetByIdAsync(id, cancellationToken);
        if (creator is null) return false;
        repository.Remove(creator);
        await repository.SaveChangesAsync(cancellationToken);
        if (creator.ImageStorageKey is not null) await images.DeleteAsync(creator.ImageStorageKey, cancellationToken);
        return true;
    }

    private CreatorDto Map(Creator creator)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return new(creator.Id, creator.Username, creator.FirstName, creator.LastName, $"{creator.FirstName} {creator.LastName}",
            ToKebabCase(creator.Category), creator.Location, creator.ImageUrl,
            creator.SocialProfiles.Select(x => new SocialProfileDto(x.Id, ToKebabCase(x.Platform), x.Url)).ToList(),
            creator.Contributions.Sum(x => x.AmountCents), creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == today).Sum(x => x.AmountCents), creator.CreatedAt);
    }

    private CreatorDto MapForDate(Creator creator, DateOnly date)
    {
        var score = creator.Contributions.Where(x => DateOnly.FromDateTime(x.ConfirmedAt.UtcDateTime) == date).Sum(x => x.AmountCents);
        var mapped = Map(creator);
        return mapped with { TotalContributedCents = score, DailyContributedCents = score };
    }

    private static string ToKebabCase<T>(T value) where T : Enum => value.ToString() switch
    {
        "AdultEntertainment" => "adult-entertainment", "BeautyFashion" => "beauty-fashion",
        "FitnessWellness" => "fitness-wellness", "ArtDesign" => "art-design", _ => value.ToString().ToLowerInvariant()
    };
}
