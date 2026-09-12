namespace CrownRank.Api.Domain;

public sealed class Creator
{
    private Creator() { }

    public Creator(Guid id, Guid entryReference, string name, string username, CreatorCategory category,
        string imageUrl, DateTimeOffset createdAt)
    {
        Id = id;
        EntryReference = entryReference;
        Name = name;
        Username = username;
        Category = category;
        ImageUrl = imageUrl;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid EntryReference { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public CreatorCategory Category { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<SocialProfile> SocialProfiles { get; private set; } = [];
    public List<Contribution> Contributions { get; private set; } = [];

    public void Update(string name, string username, CreatorCategory category, IReadOnlyCollection<SocialProfile> profiles)
    {
        Name = name;
        Username = username;
        Category = category;
        SocialProfiles.RemoveAll(existing => profiles.All(replacement => replacement.Platform != existing.Platform));
        foreach (var profile in profiles)
        {
            var existing = SocialProfiles.SingleOrDefault(x => x.Platform == profile.Platform);
            if (existing is null) SocialProfiles.Add(profile);
            else existing.UpdateUrl(profile.Url);
        }
    }

    public void Delete() => IsDeleted = true;
}

public sealed class SocialProfile
{
    private SocialProfile() { }

    public SocialProfile(Guid id, Guid creatorId, SocialPlatform platform, string url)
    {
        Id = id;
        CreatorId = creatorId;
        Platform = platform;
        Url = url;
    }

    public Guid Id { get; private set; }
    public Guid CreatorId { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string Url { get; private set; } = string.Empty;

    public void UpdateUrl(string url) => Url = url;
}

public sealed class Contribution
{
    private Contribution() { }

    public Contribution(Guid id, Guid creatorId, decimal amount, ContributionKind kind,
        DateTimeOffset confirmedAt, string paymentReference)
    {
        Id = id;
        CreatorId = creatorId;
        Amount = amount;
        Kind = kind;
        ConfirmedAt = confirmedAt;
        PaymentReference = paymentReference;
    }

    public Guid Id { get; private set; }
    public Guid CreatorId { get; private set; }
    public decimal Amount { get; private set; }
    public ContributionKind Kind { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
}

public enum ContributionKind { RankUp, Boost }

public enum SocialPlatform { Instagram, TikTok, YouTube, X, Twitch, OnlyFans, Website }

public enum CreatorCategory
{
    Streamer, Gaming, Influencer, AdultEntertainment, BeautyFashion, FitnessWellness, Music,
    Podcasting, Education, Comedy, ArtDesign, Food, Travel, Technology, Business, Other
}
