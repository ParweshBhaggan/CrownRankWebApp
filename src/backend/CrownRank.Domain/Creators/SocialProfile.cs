namespace CrownRank.Domain.Creators;

public enum SocialPlatform { Instagram, TikTok, YouTube, X, Twitch, OnlyFans, Website }

public sealed class SocialProfile
{
    private SocialProfile() { }
    internal SocialProfile(Guid id, Guid creatorId, SocialPlatform platform, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("A valid HTTPS social URL is required.", nameof(url));
        string[] hosts = platform switch
        {
            SocialPlatform.Instagram => new[] { "instagram.com" }, SocialPlatform.TikTok => ["tiktok.com"],
            SocialPlatform.YouTube => ["youtube.com", "youtu.be"], SocialPlatform.X => ["x.com", "twitter.com"],
            SocialPlatform.Twitch => ["twitch.tv"], SocialPlatform.OnlyFans => ["onlyfans.com"], _ => []
        };
        if (url.Length > 500 || !string.IsNullOrEmpty(uri.UserInfo) ||
            (hosts.Length > 0 && !hosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("The social URL must match the selected platform and contain no credentials.");
        Id = id; CreatorId = creatorId; Platform = platform; Url = uri.ToString();
    }
    public Guid Id { get; private set; }
    public Guid CreatorId { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string Url { get; private set; } = string.Empty;
}

