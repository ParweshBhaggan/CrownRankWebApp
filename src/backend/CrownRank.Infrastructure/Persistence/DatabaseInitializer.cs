using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static readonly (string Name, string Description)[] SeedCategories =
    [
        ("Streamer", "Live-streaming creators and personalities."),
        ("Gaming", "Gaming creators, players and esports personalities."),
        ("Influencer", "Lifestyle and social-media personalities."),
        ("Adult Entertainment", "Adult-oriented creators and performers."),
        ("Beauty & Fashion", "Beauty, style and fashion creators."),
        ("Fitness & Wellness", "Fitness, health and wellness creators."),
        ("Music", "Musicians, singers, producers and DJs."),
        ("Podcasting", "Podcast hosts and audio creators."),
        ("Education", "Educational creators and subject-matter experts."),
        ("Comedy", "Comedians and entertainment creators."),
        ("Art & Design", "Artists, illustrators and designers."),
        ("Food", "Food, cooking and culinary creators."),
        ("Travel", "Travel creators and explorers."),
        ("Technology", "Technology creators and reviewers."),
        ("Business", "Business, finance and entrepreneurship creators."),
        ("Other", "Creators who do not fit another category.")
    ];

    private static readonly string[] FirstNames =
        ["Maya", "Jordan", "Sofia", "Kai", "Amara", "Leo", "Nora", "Milo", "Luna", "Elias", "Zara", "Noah"];

    private static readonly string[] LastNames =
        ["Reyes", "Blake", "Chen", "Morgan", "Patel", "Woods", "Rivera", "Stone", "Bennett", "Hart", "Santos", "Vale"];

    // A tiny valid PNG. LocalProfileImageStorage still validates and normalizes it like any uploaded image.
    private const string SeedProfileImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public static async Task MigrateCrownRankAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Database.MigrateAsync(ct);
    }

    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrownRankDbContext>();
        var images = scope.ServiceProvider.GetRequiredService<IProfileImageStorage>();
        var now = DateTimeOffset.UtcNow;

        var categories = await db.Categories.ToListAsync(ct);
        foreach (var seed in SeedCategories)
        {
            if (categories.Any(x => string.Equals(x.Name, seed.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var category = Category.Create(seed.Name, seed.Description, now);
            categories.Add(category);
            db.Categories.Add(category);
        }

        var existingUsernames = (await db.Entries.Select(x => x.Username).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Two creators per category makes both global and category-specific ranking useful immediately.
        for (var index = 0; index < SeedCategories.Length * 2; index++)
        {
            var username = $"demo_creator_{index + 1:00}";
            if (existingUsernames.Contains(username)) continue;

            var categoryName = SeedCategories[index % SeedCategories.Length].Name;
            var category = categories.Single(x =>
                string.Equals(x.Name, categoryName, StringComparison.OrdinalIgnoreCase));
            var first = FirstNames[index % FirstNames.Length];
            var last = LastNames[(index * 5 + 3) % LastNames.Length];
            var createdAt = now.AddDays(-(index % 10)).AddMinutes(-index);

            await using var imageContent = new MemoryStream(Convert.FromBase64String(SeedProfileImageBase64));
            var imageKey = await images.SaveAsync(imageContent, $"seed-{index + 1}.png", ct);
            var agreement = AgreementAcceptance.Create(
                "development-seed-v1", "development-seed-v1", "development-seed-v1", createdAt);
            var links = new[]
            {
                SocialMediaLink.Create(
                    index % 3 == 0 ? SocialMediaPlatform.YouTube : SocialMediaPlatform.Instagram,
                    index % 3 == 0
                        ? $"https://youtube.com/@{username}"
                        : $"https://instagram.com/{username}")
            };

            var entry = Entry.Create(
                $"{first} {last}", username, category, imageKey, agreement, links, createdAt);
            var initial = RankingContribution.CreateInitialPayment(
                entry,
                Money.Create(2_500L + (SeedCategories.Length * 2 - index) * 575L, "EUR"),
                "development-seed",
                $"entry-{index + 1:00}",
                createdAt.AddMinutes(5));
            entry.Publish(initial);

            db.Entries.Add(entry);
            db.Contributions.Add(initial);

            if (index % 2 == 0)
            {
                db.Contributions.Add(RankingContribution.CreateBoost(
                    entry,
                    Money.Create(750L + index * 125L, "EUR"),
                    "development-seed",
                    $"boost-{index + 1:00}",
                    now.AddDays(-(index % 4)).AddMinutes(-index)));
            }

            existingUsernames.Add(username);
        }

        await db.SaveChangesAsync(ct);
    }
}
