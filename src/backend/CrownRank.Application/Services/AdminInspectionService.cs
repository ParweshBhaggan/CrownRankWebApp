using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;

namespace CrownRank.Application.Services;

public sealed class AdminInspectionService(IAdminQueries queries, IAdminAuthorization authorization)
{
    private async Task AuthorizeAsync(string credential, CancellationToken ct)
    {
        if (!await authorization.IsAuthorizedAsync(credential, ct)) throw new UnauthorizedAccessException();
    }
    public async Task<IReadOnlyList<AdminEntryRow>> EntriesAsync(string credential, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        return await queries.EntriesAsync(ct);
    }
    public async Task<IReadOnlyList<Category>> CategoriesAsync(string credential, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        return await queries.CategoriesAsync(ct);
    }
    public async Task<IReadOnlyList<AdminPaymentRow>> PaymentsAsync(string credential, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        return await queries.PaymentsAsync(ct);
    }
}
