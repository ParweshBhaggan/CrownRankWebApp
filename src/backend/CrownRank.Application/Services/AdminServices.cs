using CrownRank.Application.Abstractions;
using CrownRank.Domain.Models;

namespace CrownRank.Application.Services;

public sealed class AdminEntryService(IEntryRepository entries, ICategoryRepository categories,
    IAdminAuthorization authorization, IUnitOfWork unitOfWork, IClock clock)
{
    private async Task<Entry> AuthorizedEntryAsync(string credential, Guid id, CancellationToken ct)
    {
        if (!await authorization.IsAuthorizedAsync(credential, ct)) throw new UnauthorizedAccessException();
        return await entries.GetAsync(id, ct) ?? throw new KeyNotFoundException("Entry not found.");
    }
    public async Task UpdateDetailsAsync(string credential, Guid id, string name, string username, CancellationToken ct = default)
    {
        (await AuthorizedEntryAsync(credential, id, ct)).UpdateBasicDetails(name, username, clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task ChangeCategoryAsync(string credential, Guid id, Guid categoryId, CancellationToken ct = default)
    {
        var entry = await AuthorizedEntryAsync(credential, id, ct);
        var category = await categories.GetAsync(categoryId, ct) ?? throw new KeyNotFoundException("Category not found.");
        entry.ChangeCategory(category, clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task HideAsync(string credential, Guid id, CancellationToken ct = default)
    {
        (await AuthorizedEntryAsync(credential, id, ct)).Hide(clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task RestoreAsync(string credential, Guid id, CancellationToken ct = default)
    {
        (await AuthorizedEntryAsync(credential, id, ct)).Restore(clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task ArchiveAsync(string credential, Guid id, CancellationToken ct = default)
    {
        (await AuthorizedEntryAsync(credential, id, ct)).Archive(clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
}

public sealed class AdminCategoryService(ICategoryRepository categories, IAdminAuthorization authorization,
    IUnitOfWork unitOfWork, IClock clock)
{
    private async Task AuthorizeAsync(string credential, CancellationToken ct)
    {
        if (!await authorization.IsAuthorizedAsync(credential, ct)) throw new UnauthorizedAccessException();
    }
    public async Task<Category> CreateAsync(string credential, string name, string? description, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        var category = Category.Create(name, description, clock.UtcNow);
        await categories.AddAsync(category, ct);
        await unitOfWork.SaveAsync(ct);
        return category;
    }
    public async Task UpdateAsync(string credential, Guid id, string name, string? description, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        (await categories.GetAsync(id, ct) ?? throw new KeyNotFoundException("Category not found."))
            .Update(name, description, clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task ArchiveAsync(string credential, Guid id, CancellationToken ct = default)
    {
        await AuthorizeAsync(credential, ct);
        (await categories.GetAsync(id, ct) ?? throw new KeyNotFoundException("Category not found."))
            .Archive(clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
}
