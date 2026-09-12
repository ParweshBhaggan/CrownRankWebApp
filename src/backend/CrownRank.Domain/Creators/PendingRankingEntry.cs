namespace CrownRank.Domain.Creators;

public sealed class PendingRankingEntry
{
    private PendingRankingEntry() { }

    public PendingRankingEntry(
        Guid referenceId,
        Guid creatorId,
        string name,
        string username,
        CreatorCategory category,
        decimal amount,
        string imageUrl,
        string? imageStorageKey,
        DateTimeOffset createdAt,
        IReadOnlyCollection<(SocialPlatform Platform, string Url)> socialProfiles)
    {
        if (referenceId == Guid.Empty || creatorId == Guid.Empty)
            throw new ArgumentException("Checkout and creator references are required.");
        Money.Validate(amount);
        if (socialProfiles.Count is < 1 or > 5)
            throw new ArgumentException("Provide between one and five social profiles.");

        ReferenceId = referenceId;
        CreatorId = creatorId;
        Name = Required(name, nameof(name), 160);
        Username = Required(username, nameof(username), 50).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(Username, @"^[a-z0-9._-]{2,40}$"))
            throw new ArgumentException("Use 2–40 letters, numbers, dots, underscores, or dashes.");
        Category = category;
        Amount = amount;
        ImageUrl = Required(imageUrl, nameof(imageUrl), 500);
        ImageStorageKey = imageStorageKey;
        CreatedAt = createdAt;
        foreach (var profile in socialProfiles)
            AddSocialProfile(profile.Platform, profile.Url);
    }

    public Guid ReferenceId { get; private set; }
    public Guid CreatorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public CreatorCategory Category { get; private set; }
    public decimal Amount { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string? ImageStorageKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<PendingRankingEntrySocialProfile> SocialProfiles => _socialProfiles;

    private readonly List<PendingRankingEntrySocialProfile> _socialProfiles = [];

    public bool Matches(
        string name,
        string username,
        CreatorCategory category,
        decimal amount,
        IReadOnlyCollection<(SocialPlatform Platform, string Url)> socialProfiles)
    {
        if (Name != name.Trim() || Username != username.Trim().TrimStart('@').ToLowerInvariant() ||
            Category != category || Amount != amount || SocialProfiles.Count != socialProfiles.Count)
            return false;
        return socialProfiles.All(candidate => SocialProfiles.Any(stored =>
            stored.Platform == candidate.Platform &&
            stored.Url == SocialProfile.NormalizeUrl(candidate.Platform, candidate.Url)));
    }

    public Creator Confirm(DateTimeOffset confirmedAt, string paymentReference)
    {
        var creator = new Creator(CreatorId, Name, Username, Category, ImageUrl, ImageStorageKey, confirmedAt);
        foreach (var profile in _socialProfiles)
            creator.AddSocialProfile(profile.Platform, profile.Url);
        creator.PrepareEntry(ReferenceId, Amount);
        creator.AddContribution(Amount, ContributionKind.RankUp, confirmedAt, paymentReference);
        return creator;
    }

    private void AddSocialProfile(SocialPlatform platform, string url)
    {
        if (_socialProfiles.Any(x => x.Platform == platform))
            throw new ArgumentException("Each social platform can be added only once.", nameof(platform));
        _socialProfiles.Add(new PendingRankingEntrySocialProfile(
            Guid.NewGuid(), ReferenceId, platform, SocialProfile.NormalizeUrl(platform, url)));
    }

    private static string Required(string value, string name, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} is required.", name)
            : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException($"{name} is too long.", name);
    }
}

public sealed class PendingRankingEntrySocialProfile
{
    private PendingRankingEntrySocialProfile() { }

    internal PendingRankingEntrySocialProfile(
        Guid id,
        Guid pendingRankingEntryReferenceId,
        SocialPlatform platform,
        string url)
    {
        Id = id;
        PendingRankingEntryReferenceId = pendingRankingEntryReferenceId;
        Platform = platform;
        Url = url;
    }

    public Guid Id { get; private set; }
    public Guid PendingRankingEntryReferenceId { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string Url { get; private set; } = string.Empty;
}
