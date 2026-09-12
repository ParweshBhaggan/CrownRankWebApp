using CrownRank.Domain.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static readonly string[] Names = ["Maya Reyes", "Jordan Blake", "Sofia Chen", "Kai Morgan", "Amara Patel", "Leo Woods", "Nora Rivera", "Milo Stone", "Luna Bennett", "Elias Hart"];

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrownRankDbContext>();
        // Schema changes and database updates are performed manually by the owner.
        if (await db.Creators.AnyAsync(cancellationToken)) return;
        var now = DateTimeOffset.UtcNow; var categories = Enum.GetValues<CreatorCategory>();
        for (var index = 0; index < 50; index++)
        {
            var name = Names[index % Names.Length];
            var creator = new Creator(Guid.NewGuid(), name, $"creator{index + 1}", categories[index % categories.Length],
                $"https://i.pravatar.cc/1024?img={(index % 70) + 1}", null, now.AddDays(-index - 1));
            creator.AddSocialProfile(index % 4 == 0 ? SocialPlatform.Twitch : SocialPlatform.Instagram,
                index % 4 == 0 ? $"https://twitch.tv/{creator.Username}" : $"https://instagram.com/{creator.Username}");
            if (index % 3 == 0) creator.AddSocialProfile(SocialPlatform.YouTube, $"https://youtube.com/@{creator.Username}");
            creator.AddContribution(((50 - index) * 12_500m + index * 173m) / 100m, ContributionKind.RankUp, now.AddDays(-(index % 12)), $"seed-rank-{index + 1}");
            creator.AddContribution((index % 9 + 1) * 11m, ContributionKind.Boost, now.AddHours(-(index % 23)), $"seed-boost-{index + 1}");
            db.Creators.Add(creator);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
