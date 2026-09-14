using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace CrownRank.Infrastructure.Persistence;

public sealed class EfAdminQueries(CrownRankDbContext db) : IAdminQueries
{
    public async Task<IReadOnlyList<AdminEntryRow>> EntriesAsync(CancellationToken ct = default)
        => await db.Entries.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AdminEntryRow(x.Id, x.Name, x.Username, x.CategoryId, x.Status, x.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Category>> CategoriesAsync(CancellationToken ct = default)
        => await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<AdminPaymentRow>> PaymentsAsync(CancellationToken ct = default)
        => await db.PaymentAttempts.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AdminPaymentRow(x.Id, x.EntryId, x.Purpose, x.State,
                x.ExpectedAmount.AmountInMinorUnits, x.ExpectedAmount.Currency, x.Provider, x.Reference))
            .ToListAsync(ct);
}
