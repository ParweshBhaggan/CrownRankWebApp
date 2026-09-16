using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;

namespace CrownRank.Application.Services;

public sealed class AdminInspectionService(IAdminQueries queries)
{
    public Task<IReadOnlyList<AdminEntryRow>> EntriesAsync(CancellationToken ct = default) => queries.EntriesAsync(ct);
    public Task<IReadOnlyList<Category>> CategoriesAsync(CancellationToken ct = default) => queries.CategoriesAsync(ct);
    public Task<IReadOnlyList<AdminPaymentRow>> PaymentsAsync(CancellationToken ct = default) => queries.PaymentsAsync(ct);
}
