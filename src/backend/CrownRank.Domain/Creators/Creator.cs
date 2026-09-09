namespace CrownRank.Domain.Creators;

public sealed class Creator
{
    private Creator() { }

    public Creator(Guid id, string firstName, string lastName, string username, CreatorCategory category,
        string imageUrl, string? imageStorageKey, DateTimeOffset createdAt)
    {
        Id = id;
        FirstName = Required(firstName, nameof(firstName), 80);
        LastName = Required(lastName, nameof(lastName), 80);
        Username = Required(Required(username, nameof(username), 51).TrimStart('@'), nameof(username), 50).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(Username, @"^[a-z0-9._-]{2,40}$")) throw new ArgumentException("Use 2–40 letters, numbers, dots, underscores, or dashes.");
        Category = category;
        ImageUrl = Required(imageUrl, nameof(imageUrl), 500);
        ImageStorageKey = imageStorageKey;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public CreatorCategory Category { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? ImageStorageKey { get; private set; }
    public bool IsHidden { get; private set; }
    public Guid? EntryReference { get; private set; }
    public decimal OpeningAmount { get; private set; }
    public void PrepareEntry(Guid reference, decimal amount) { Money.Validate(amount); EntryReference = reference; OpeningAmount = amount; }
    public void RecoverEntry(Guid reference)
    {
        if (_contributions.Count > 0) throw new InvalidOperationException("A confirmed entry cannot be reassigned.");
        if (reference == Guid.Empty) throw new ArgumentException("An entry reference is required.", nameof(reference));
        EntryReference = reference;
    }
    public void Hide() => IsHidden = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<SocialProfile> SocialProfiles => _socialProfiles;
    public IReadOnlyCollection<Contribution> Contributions => _contributions;

    private readonly List<SocialProfile> _socialProfiles = [];
    private readonly List<Contribution> _contributions = [];

    public void AddSocialProfile(SocialPlatform platform, string url)
    {
        if (_socialProfiles.Any(x => x.Platform == platform))
            throw new ArgumentException("Each social platform can be added only once.", nameof(platform));
        _socialProfiles.Add(new SocialProfile(Guid.NewGuid(), Id, platform, url));
    }

    public void AddContribution(decimal amount, ContributionKind kind, DateTimeOffset confirmedAt, string reference)
    {
        Money.Validate(amount);
        if (_contributions.Any(x => x.PaymentReference == reference)) return;
        _contributions.Add(new Contribution(Guid.NewGuid(), Id, amount, kind, confirmedAt, reference));
    }

    private static string Required(string value, string name, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
        return normalized.Length <= maxLength ? normalized : throw new ArgumentException($"{name} is too long.", name);
    }
}
