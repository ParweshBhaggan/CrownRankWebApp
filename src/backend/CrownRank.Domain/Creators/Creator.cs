namespace CrownRank.Domain.Creators;

public sealed class Creator
{
    private Creator() { }

    public Creator(Guid id, string firstName, string lastName, string username, CreatorCategory category,
        string imageUrl, string? imageStorageKey, string? location, DateTimeOffset createdAt)
    {
        Id = id;
        FirstName = Required(firstName, nameof(firstName), 80);
        LastName = Required(lastName, nameof(lastName), 80);
        Username = Required(Required(username, nameof(username), 51).TrimStart('@'), nameof(username), 50).ToLowerInvariant();
        Category = category;
        ImageUrl = Required(imageUrl, nameof(imageUrl), 500);
        ImageStorageKey = imageStorageKey;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public CreatorCategory Category { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? ImageStorageKey { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<SocialProfile> SocialProfiles => _socialProfiles;
    public IReadOnlyCollection<Contribution> Contributions => _contributions;

    private readonly List<SocialProfile> _socialProfiles = [];
    private readonly List<Contribution> _contributions = [];

    public void AddSocialProfile(SocialPlatform platform, string url) =>
        _socialProfiles.Add(new SocialProfile(Guid.NewGuid(), Id, platform, url));

    public void AddContribution(long amountCents, ContributionKind kind, DateTimeOffset confirmedAt, string reference)
    {
        if (amountCents < 100) throw new ArgumentOutOfRangeException(nameof(amountCents), "Minimum amount is one currency unit.");
        _contributions.Add(new Contribution(Guid.NewGuid(), Id, amountCents, kind, confirmedAt, reference));
    }

    private static string Required(string value, string name, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
        return normalized.Length <= maxLength ? normalized : throw new ArgumentException($"{name} is too long.", name);
    }
}
