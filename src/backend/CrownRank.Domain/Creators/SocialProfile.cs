namespace CrownRank.Domain.Creators;

public enum SocialPlatform { Instagram, TikTok, YouTube, X, Twitch, OnlyFans, Website }

public sealed class SocialProfile
{
    private SocialProfile() { }
    internal SocialProfile(Guid id, Guid creatorId, SocialPlatform platform, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("A valid HTTPS social URL is required.", nameof(url));
        Id = id; CreatorId = creatorId; Platform = platform; Url = uri.ToString();
    }
    public Guid Id { get; private set; }
    public Guid CreatorId { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string Url { get; private set; } = string.Empty;
}
