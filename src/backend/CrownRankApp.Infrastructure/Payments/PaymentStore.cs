using System.Text.Json;
using CrownRankApp.Application.Payments;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class PaymentStore(ApplicationDbContext context, ProfileImageStorage images, TimeProvider clock) : IPaymentStore
{
    public Task<PaymentOperation?> FindAsync(Guid id, CancellationToken ct)
    {
        return context.PaymentOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == id, ct);
    }

    public async Task<PaymentOperation> ReserveAsync(PaymentOperation operation, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        if (operation.Purpose == PaymentPurpose.Boost)
        {
            // Serialize starting boosts with deletion of the target entry.
            var locked = await context.Entries.Where(target => target.Id == operation.EntryId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(target => target.Score, target => target.Score), ct);
            if (locked == 0)
            {
                throw new ArgumentException("This creator is no longer available.");
            }
            var target = await context.Entries.AsNoTracking().SingleAsync(target => target.Id == operation.EntryId, ct);
            if (target.Score > 9999999999999999.99m - operation.Amount)
            {
                throw new ArgumentException("This boost would exceed the maximum score.");
            }
            operation.Description = $"CrownRank boost: {target.Name}";
        }
        context.PaymentOperations.Add(operation);
        try
        {
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            context.ChangeTracker.Clear();
            return operation;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            var existing = await FindAsync(operation.Id, ct);
            if (existing is not null && existing.RequestHash == operation.RequestHash && existing.Purpose == operation.Purpose)
            {
                return existing;
            }
            throw new InvalidOperationException("This reference was already used with different checkout details.");
        }
    }

    public async Task RegisterEntryAsync(Guid id, EntryRegistrationRequest entry, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var savedImage = false;
        try
        {
            // Lock the paid operation; profile creation and payment consumption commit together.
            var locked = await context.PaymentOperations.Where(payment => payment.Id == id && payment.Purpose == PaymentPurpose.Entry
                    && payment.Status == PaymentStatus.Paid && payment.FulfilledAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(payment => payment.Status, PaymentStatus.Paid), ct);
            var payment = await context.PaymentOperations.SingleAsync(payment => payment.Id == id, ct);
            if (payment.FulfilledAt is not null)
            {
                await transaction.CommitAsync(ct);
                return;
            }
            if (locked == 0)
            {
                throw new InvalidOperationException("A verified entry payment is required.");
            }
            if (payment.EntryJson is null)
            {
                if (await context.Entries.AnyAsync(existing => existing.Username.ToLower() == entry.Username, ct))
                {
                    throw new InvalidOperationException("This username is taken. Choose another username for your paid entry.");
                }
                var categoryNames = entry.Categories.Select(category => category.Name.Trim()).Distinct().ToList();
                var categories = await context.Categories.Where(category => categoryNames.Contains(category.Name)).OrderBy(category => category.Id).ToListAsync(ct);
                if (categories.Count != categoryNames.Count)
                {
                    throw new ArgumentException("A selected category is unavailable. Choose another category for your paid entry.");
                }
                foreach (var category in categories)
                {
                    if (await context.Categories.Where(value => value.Id == category.Id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Name, value => value.Name), ct) != 1)
                    {
                        throw new ArgumentException("A selected category is unavailable.");
                    }
                }
                var platformNames = entry.SocialMediaPlatforms.Select(profile => profile.PlatformName.Trim()).ToList();
                var platforms = await context.SocialMediaDefaults.Where(platform => platformNames.Contains(platform.Name)).OrderBy(platform => platform.Id).ToListAsync(ct);
                if (platforms.Count != platformNames.Count)
                {
                    throw new ArgumentException("A selected social platform is unavailable.");
                }
                foreach (var platform in platforms)
                {
                    if (await context.SocialMediaDefaults.Where(value => value.Id == platform.Id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Name, value => value.Name), ct) != 1)
                    {
                        throw new ArgumentException("A selected social platform is unavailable.");
                    }
                }
                var profiles = new List<SocialProfileRequest>();
                foreach (var profile in entry.SocialMediaPlatforms)
                {
                    var platform = platforms.Single(platform => platform.Name == profile.PlatformName.Trim());
                    ValidateSocialHost(platform.Name, profile.Url);
                    profiles.Add(new SocialProfileRequest(platform.Id, profile.Url));
                }
                var imageUrl = await images.SaveAsync(id, entry.ImgUrl, ct);
                savedImage = true;
                payment.EntryJson = JsonSerializer.Serialize(new StoredEntry(entry.Name, entry.Username, categories[0].Id, profiles, imageUrl, categories.Select(category => category.Id).ToList()));
                payment.ReservedUsername = entry.Username;
                payment.AgreementsAcceptedAt = clock.GetUtcNow().UtcDateTime;
                await context.SaveChangesAsync(ct);
            }
            await FulfillCoreAsync(id, ct);
            await transaction.CommitAsync(ct);
            context.ChangeTracker.Clear();
        }
        catch (Exception exception)
        {
            // Remove an uncommitted image while the operation is still locked against another retry.
            if (savedImage)
            {
                images.Delete(id);
            }
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            if (exception is DbUpdateException { InnerException: PostgresException databaseError }
                && databaseError.SqlState == PostgresErrorCodes.UniqueViolation
                && databaseError.ConstraintName is "IX_Entries_Username" or "IX_PaymentOperations_ReservedUsername")
            {
                throw new InvalidOperationException("This username is taken. Choose another username for your paid entry.", exception);
            }
            throw;
        }
    }

    public async Task AttachSessionAsync(Guid id, VerifiedCheckout checkout, CancellationToken ct)
    {
        if (id != checkout.OperationId)
        {
            throw new InvalidOperationException("Checkout operation mismatch.");
        }
        var changed = await context.PaymentOperations.Where(operation => operation.Id == id
                && (operation.SessionId == null || operation.SessionId == checkout.SessionId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.SessionId, checkout.SessionId)
                .SetProperty(operation => operation.CheckoutUrl, checkout.Url), ct);
        if (changed != 1)
        {
            throw new InvalidOperationException("This payment already has a different checkout session.");
        }
    }

    public async Task ApplyAsync(VerifiedCheckout checkout, CancellationToken ct)
    {
        await AttachSessionAsync(checkout.OperationId, checkout, ct);
        if (!checkout.Paid)
        {
            if (checkout.Status == "expired" || checkout.Failed)
            {
                await context.PaymentOperations.Where(operation => operation.Id == checkout.OperationId
                        && operation.Status != PaymentStatus.Paid && operation.FulfilledAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, checkout.Failed ? PaymentStatus.Failed : PaymentStatus.Expired)
                        .SetProperty(operation => operation.ReservedUsername, (string?)null), ct);
            }
            else if (checkout.Status == "complete")
            {
                await context.PaymentOperations.Where(operation => operation.Id == checkout.OperationId
                        && operation.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, PaymentStatus.Processing), ct);
            }
            return;
        }
        var paidAt = checkout.PaidAt ?? throw new InvalidOperationException("Verified paid checkout has no transaction timestamp.");
        if (checkout.PaymentIntentId is null)
        {
            throw new InvalidOperationException("Verified paid checkout has no payment intent.");
        }
        // Persist payment success before fulfillment. A later database failure remains recoverable and visible.
        await context.PaymentOperations.Where(operation => operation.Id == checkout.OperationId && operation.PaidAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, PaymentStatus.Paid)
                .SetProperty(operation => operation.PaidAt, paidAt)
                .SetProperty(operation => operation.PaymentIntentId, checkout.PaymentIntentId), ct);
        await FulfillAsync(checkout.OperationId, ct);
    }

    private async Task FulfillAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await FulfillCoreAsync(id, ct);
        await transaction.CommitAsync(ct);
        context.ChangeTracker.Clear();
    }

    private async Task FulfillCoreAsync(Guid id, CancellationToken ct)
    {
        var claimed = await context.PaymentOperations.Where(operation => operation.Id == id && operation.Status == PaymentStatus.Paid && operation.FulfilledAt == null
                && (operation.Purpose == PaymentPurpose.Boost || operation.EntryJson != null))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.FulfilledAt, clock.GetUtcNow().UtcDateTime), ct);
        if (claimed == 0)
        {
            return;
        }
        var payment = await context.PaymentOperations.SingleAsync(operation => operation.Id == id, ct);
        var timestamp = payment.PaidAt!.Value;
        Guid entryId;
        if (payment.Purpose == PaymentPurpose.Entry)
        {
            var details = JsonSerializer.Deserialize<StoredEntry>(payment.EntryJson!)!;
            var categoryIds = details.CategoryIds ?? [details.CategoryId];
            var categories = await context.Categories.Where(category => categoryIds.Contains(category.Id)).ToListAsync(ct);
            if (categories.Count != categoryIds.Count)
            {
                throw new InvalidOperationException("A paid entry category is unavailable.");
            }
            var entry = new Entry(payment.Id)
            {
                Name = details.Name,
                Username = details.Username,
                ImgUrl = details.ImageUrl,
                Score = payment.Amount,
                CreatedDate = timestamp,
                Categories = categories
            };
            foreach (var profile in details.SocialProfiles)
            {
                var platform = await context.SocialMediaDefaults.SingleAsync(platform => platform.Id == profile.PlatformId, ct);
                entry.SocialMediaPlatforms.Add(new SocialMediaPlatform
                    {
                        Entry = entry,
                        Platform = platform,
                        PlatformId = platform.Id,
                        Url = profile.Url
                    });
            }
            context.Entries.Add(entry);
            entryId = entry.Id;
        }
        else
        {
            entryId = payment.EntryId!.Value;
            var updated = await context.Entries.Where(entry => entry.Id == entryId && entry.Score <= 9999999999999999.99m - payment.Amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Score, entry => entry.Score + payment.Amount)
                    .SetProperty(entry => entry.UpdatedDate,
                    entry => (entry.UpdatedDate ?? entry.CreatedDate) > timestamp ? (entry.UpdatedDate ?? entry.CreatedDate) : timestamp), ct);
            if (updated != 1)
            {
                throw new InvalidOperationException("Paid boost needs administrator attention: entry unavailable or score limit reached.");
            }
        }
        context.ScoreAdditions.Add(new ScoreAddition(payment.Id)
            {
                EntryId = entryId,
                Amount = payment.Amount,
                CreatedDate = timestamp
            });
        payment.EntryId = entryId;
        payment.ReservedUsername = null;
        await context.SaveChangesAsync(ct);
    }

    public Task ExpireUncreatedAsync(Guid id, CancellationToken ct)
    {
        return context.PaymentOperations.Where(operation => operation.Id == id
                && operation.SessionId == null && operation.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, PaymentStatus.Expired)
                .SetProperty(operation => operation.ReservedUsername, (string?)null), ct);
    }

    public async Task<List<PaymentOperation>> GetPendingAsync(CancellationToken ct)
    {
        var imageCutoff = clock.GetUtcNow().UtcDateTime.AddDays(-7);
        var abandoned = await context.PaymentOperations.AsNoTracking().Where(operation => operation.Status == PaymentStatus.Expired
                && operation.Purpose == PaymentPurpose.Entry && operation.CreatedDate < imageCutoff && operation.EntryJson != null).ToListAsync(ct);
        foreach (var operation in abandoned)
        {
            images.Delete(operation.Id);
            await context.PaymentOperations.Where(payment => payment.Id == operation.Id && payment.Status == PaymentStatus.Expired)
                .ExecuteUpdateAsync(setters => setters.SetProperty(payment => payment.EntryJson, (string?)null), ct);
        }
        var pending = await context.PaymentOperations.AsNoTracking().Where(operation => operation.FulfilledAt == null
                && (operation.Status == PaymentStatus.Pending || operation.Status == PaymentStatus.Processing
                || (operation.Status == PaymentStatus.Paid && (operation.Purpose == PaymentPurpose.Boost || operation.EntryJson != null))))
            .OrderBy(operation => operation.LastCheckedAt ?? operation.CreatedDate).Take(100).ToListAsync(ct);
        var ids = pending.Select(operation => operation.Id).ToList();
        await context.PaymentOperations.Where(operation => ids.Contains(operation.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.LastCheckedAt, clock.GetUtcNow().UtcDateTime), ct);
        return pending;
    }

    private static void ValidateSocialHost(string platform, string url)
    {
        string[] allowed;
        switch (platform.ToLowerInvariant())
        {
                case "instagram":
                {
                    allowed = ["instagram.com"];
                    break;
                }
                case "tiktok":
                {
                    allowed = ["tiktok.com"];
                    break;
                }
                case "youtube":
                {
                    allowed = ["youtube.com", "youtu.be"];
                    break;
                }
                case "onlyfans":
                {
                    allowed = ["onlyfans.com"];
                    break;
                }
                case "facebook":
                {
                    allowed = ["facebook.com", "fb.com"];
                    break;
                }
                case "x":
                case "twitter":
                {
                    allowed = ["x.com", "twitter.com"];
                    break;
                }
                case "twitch":
                {
                    allowed = ["twitch.tv"];
                    break;
                }
                default:
                {
                    allowed = [];
                    break;
                }
        }
        var host = new Uri(url).Host;
        if (allowed.Length > 0 && !allowed.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A social URL does not match its selected platform.");
        }
    }

    private sealed record StoredEntry(string Name, string Username, Guid CategoryId, List<SocialProfileRequest> SocialProfiles, string ImageUrl, List<Guid>? CategoryIds = null);
}
