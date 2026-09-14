using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Domain.Models;

namespace CrownRank.Application.Services;

public sealed class AdminEntryService(IEntryRepository entries, ICategoryRepository categories,
    IAdminAuthorization authorization, IUnitOfWork unitOfWork, IProfileImageStorage images, IClock clock)
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
    public async Task ReplaceSocialLinksAsync(string credential, Guid id, IReadOnlyList<SocialLinkInput> links, CancellationToken ct = default)
    {
        var entry = await AuthorizedEntryAsync(credential, id, ct);
        entry.ReplaceSocialMediaLinks(links.Select(x => SocialMediaLink.Create(x.Platform, x.Url, x.CustomPlatformName)), clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
    public async Task ReplaceImageAsync(string credential, Guid id, Stream content, string fileName, CancellationToken ct = default)
    {
        var entry = await AuthorizedEntryAsync(credential, id, ct);
        var previous = entry.ProfileImageKey;
        var key = await images.SaveAsync(content, fileName, ct);
        try
        {
            entry.UpdateProfileImage(key, clock.UtcNow);
            await unitOfWork.SaveAsync(ct);
        }
        catch
        {
            await images.DeleteAsync(key, ct);
            throw;
        }
        try { await images.DeleteAsync(previous, ct); }
        catch (IOException) { /* The persisted reference is correct; orphan cleanup can run later. */ }
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

public sealed class AdminCategoryService(ICategoryRepository categories, IAdminQueries queries, IAdminAuthorization authorization,
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
        if ((await queries.EntriesAsync(ct)).Any(e => e.CategoryId == id && e.Status != EntryStatus.Archived))
            throw new InvalidOperationException("Move or archive the category's entries before archiving it.");
        (await categories.GetAsync(id, ct) ?? throw new KeyNotFoundException("Category not found."))
            .Archive(clock.UtcNow);
        await unitOfWork.SaveAsync(ct);
    }
}
