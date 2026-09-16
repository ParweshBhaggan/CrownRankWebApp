using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

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

    public async Task<BoostPreview> BoostPreviewAsync(Guid entryId, Money amount, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.AmountInMinorUnits <= 0) throw new ArgumentException("Boost amount must be positive.", nameof(amount));
        var entry = await ProfileAsync(entryId, ct) ?? throw new KeyNotFoundException("Public entry not found.");
        var row = (await leaderboards.GetGlobalAsync(entry.CategoryId, ct)).Single(x => x.EntryId == entryId);
        if (!string.Equals(row.Currency, amount.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException("Boost currency must match the leaderboard currency.");
        return new BoostPreview(entry.Id, entry.Name, entry.Username, row.Rank, row.ScoreInMinorUnits,
            amount.AmountInMinorUnits, checked(row.ScoreInMinorUnits + amount.AmountInMinorUnits), row.Currency);
    }
}
