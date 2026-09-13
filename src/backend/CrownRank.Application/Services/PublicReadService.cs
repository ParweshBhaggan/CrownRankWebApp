using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;

namespace CrownRank.Application.Services;

public sealed class PublicReadService(ICategoryRepository categories, IEntryRepository entries, ILeaderboardQueries leaderboards)
{
    public Task<IReadOnlyList<Category>> CategoriesAsync(CancellationToken ct = default) => categories.ListActiveAsync(ct);
    public async Task<Entry?> ProfileAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await entries.GetAsync(id, ct);
        return entry?.Status == EntryStatus.Published ? entry : null;
    }
    public Task<IReadOnlyList<LeaderboardRow>> GlobalAsync(Guid? categoryId = null, CancellationToken ct = default)
        => leaderboards.GetGlobalAsync(categoryId, ct);
    public Task<IReadOnlyList<LeaderboardRow>> DailyAsync(DateOnly utcDay, Guid? categoryId = null, CancellationToken ct = default)
        => leaderboards.GetDailyAsync(utcDay, categoryId, ct);
}
