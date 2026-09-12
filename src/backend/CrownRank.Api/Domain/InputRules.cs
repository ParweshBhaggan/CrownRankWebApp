using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using CrownRank.Api.Contracts;
using CrownRank.Api.Infrastructure;

namespace CrownRank.Api.Domain;

public static class InputRules
{
    public static string Name(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 160)
            BadRequest("Name is required and must not exceed 160 characters.");
        return normalized;
    }

    public static string Username(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, "^[a-z0-9._-]{2,40}$"))
            BadRequest("Username must contain 2–40 letters, numbers, dots, underscores, or dashes.");
        return normalized;
    }

    public static decimal Money(decimal amount)
    {
        if (amount is < 1m or > 10_000m || decimal.Round(amount, 2) != amount)
            BadRequest("Amount must be between 1.00 and 10000.00 with at most two decimal places.");
        return amount;
    }

    public static CreatorCategory Category(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "streamer" => CreatorCategory.Streamer,
        "gaming" => CreatorCategory.Gaming,
        "influencer" => CreatorCategory.Influencer,
        "adult-entertainment" => CreatorCategory.AdultEntertainment,
        "beauty-fashion" => CreatorCategory.BeautyFashion,
        "fitness-wellness" => CreatorCategory.FitnessWellness,
        "music" => CreatorCategory.Music,
        "podcasting" => CreatorCategory.Podcasting,
        "education" => CreatorCategory.Education,
        "comedy" => CreatorCategory.Comedy,
        "art-design" => CreatorCategory.ArtDesign,
        "food" => CreatorCategory.Food,
        "travel" => CreatorCategory.Travel,
        "technology" => CreatorCategory.Technology,
        "business" => CreatorCategory.Business,
        "other" => CreatorCategory.Other,
        _ => throw Problem("Select a valid creator category.")
    };

    public static IReadOnlyList<SocialProfileInput> SocialProfilesFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<SocialProfileInput>>(json,
                       new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? throw Problem("Social profiles are required.");
        }
        catch (JsonException)
        {
            throw Problem("Social profiles must be valid JSON.");
        }
    }

    public static List<SocialProfile> SocialProfiles(Guid creatorId, IReadOnlyList<SocialProfileInput> inputs)
    {
        if (inputs.Count is < 1 or > 5) BadRequest("Provide between one and five social profiles.");
        var result = new List<SocialProfile>(inputs.Count);
        foreach (var input in inputs)
        {
            var platform = Platform(input.Platform);
            if (result.Any(x => x.Platform == platform)) BadRequest("Each social platform may be supplied once.");
            result.Add(new SocialProfile(Guid.NewGuid(), creatorId, platform, NormalizeUrl(platform, input.Url)));
        }
        return result;
    }

    public static string CategorySlug(CreatorCategory category) => category switch
    {
        CreatorCategory.AdultEntertainment => "adult-entertainment",
        CreatorCategory.BeautyFashion => "beauty-fashion",
        CreatorCategory.FitnessWellness => "fitness-wellness",
        CreatorCategory.ArtDesign => "art-design",
        _ => category.ToString().ToLowerInvariant()
    };

    public static string PlatformSlug(SocialPlatform platform) => platform switch
    {
        SocialPlatform.TikTok => "tiktok",
        SocialPlatform.YouTube => "youtube",
        SocialPlatform.OnlyFans => "onlyfans",
        _ => platform.ToString().ToLowerInvariant()
    };

    private static SocialPlatform Platform(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "instagram" => SocialPlatform.Instagram,
        "tiktok" => SocialPlatform.TikTok,
        "youtube" => SocialPlatform.YouTube,
        "x" => SocialPlatform.X,
        "twitch" => SocialPlatform.Twitch,
        "onlyfans" => SocialPlatform.OnlyFans,
        "website" => SocialPlatform.Website,
        _ => throw Problem("Select a valid social platform.")
    };

    private static string NormalizeUrl(SocialPlatform platform, string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            BadRequest("Social profile URLs must be absolute HTTPS URLs.");

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        var validHost = platform switch
        {
            SocialPlatform.Instagram => IsHost(host, "instagram.com"),
            SocialPlatform.TikTok => IsHost(host, "tiktok.com"),
            SocialPlatform.YouTube => IsHost(host, "youtube.com") || IsHost(host, "youtu.be"),
            SocialPlatform.X => IsHost(host, "x.com") || IsHost(host, "twitter.com"),
            SocialPlatform.Twitch => IsHost(host, "twitch.tv"),
            SocialPlatform.OnlyFans => IsHost(host, "onlyfans.com"),
            SocialPlatform.Website => true,
            _ => false
        };
        if (!validHost) BadRequest("The social profile URL does not match its selected platform.");
        return uri.AbsoluteUri;
    }

    private static bool IsHost(string actual, string expected) =>
        actual == expected || actual.EndsWith('.' + expected, StringComparison.Ordinal);

    private static ApiProblemException Problem(string detail) =>
        new(StatusCodes.Status400BadRequest, "Invalid request", detail);

    [DoesNotReturn]
    private static void BadRequest(string detail) => throw Problem(detail);
}
