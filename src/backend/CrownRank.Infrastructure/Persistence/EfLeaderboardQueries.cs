using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.Persistence;

public sealed class EfLeaderboardQueries(CrownRankDbContext db) : ILeaderboardQueries
{
    public Task<IReadOnlyList<LeaderboardRow>> GetGlobalAsync(Guid? categoryId, CancellationToken ct = default)
        => QueryAsync(null, categoryId, ct);

    public Task<IReadOnlyList<LeaderboardRow>> GetDailyAsync(DateOnly utcDay, Guid? categoryId, CancellationToken ct = default)
        => QueryAsync(utcDay, categoryId, ct);

    private async Task<IReadOnlyList<LeaderboardRow>> QueryAsync(DateOnly? day, Guid? categoryId, CancellationToken ct)
    {
        var entries = db.Entries.AsNoTracking().Where(x => x.Status == EntryStatus.Published);
        if (categoryId.HasValue) entries = entries.Where(x => x.CategoryId == categoryId.Value);
        var contributions = db.Contributions.AsNoTracking().Where(x => x.Status == ContributionStatus.Active);
        if (day.HasValue)
        {
            var start = new DateTimeOffset(day.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(1);
            contributions = contributions.Where(x => x.ConfirmedAtUtc >= start && x.ConfirmedAtUtc < end);
        }

        // Restrict rows in SQL before grouping. Monetary totals and tie breaks use checked integer arithmetic in .NET.
        var rows = await (from c in contributions
                          join e in entries on c.EntryId equals e.Id
                          select new { e.Id, e.Name, e.Username, e.CategoryId,
                              Amount = c.Amount.AmountInMinorUnits, c.Amount.Currency, c.ConfirmedAtUtc })
            .ToListAsync(ct);
        var scored = rows.GroupBy(x => x.Id).Select(group =>
        {
            var currencies = group.Select(x => x.Currency).Distinct(StringComparer.Ordinal).ToArray();
            if (currencies.Length != 1) throw new InvalidOperationException("Contributions in a leaderboard must use one currency.");
            var first = group.First();
            return new
            {
                Entry = first,
                Score = group.Aggregate(0L, (sum, row) => checked(sum + row.Amount)),
                Currency = currencies[0],
                Reached = group.Max(x => x.ConfirmedAtUtc)
            };
        }).ToArray();
        if (scored.Select(x => x.Currency).Distinct(StringComparer.Ordinal).Skip(1).Any())
            throw new InvalidOperationException("A leaderboard cannot compare amounts in different currencies.");
        return scored.OrderByDescending(x => x.Score).ThenBy(x => x.Reached).ThenBy(x => x.Entry.Id)
            .Select((x, index) => new LeaderboardRow(x.Entry.Id, x.Entry.Name, x.Entry.Username,
                x.Entry.CategoryId, x.Score, x.Currency, x.Reached, index + 1)).ToArray();
    }
}
