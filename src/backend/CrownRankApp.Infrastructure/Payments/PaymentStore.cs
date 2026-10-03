using System.Text.Json;
using CrownRankApp.Application.Payments;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class PaymentStore(ApplicationDbContext context, ProfileImageStorage images, TimeProvider clock) : IPaymentStore
{
    public Task<PaymentOperation?> FindAsync(Guid id, CancellationToken ct)
    {
        return context.PaymentOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == id, ct);
    }

    public async Task<PaymentOperation> ReserveAsync(PaymentOperation operation, EntrySubmissionRequest? entry, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        if (entry is not null)
        {
            if (await context.Entries.AnyAsync(existing => existing.Username.ToLower() == entry.Username, ct))
            {
                throw new InvalidOperationException("This username already has a ranked entry.");
            }
            if (await context.Categories.Where(category => category.Id == entry.CategoryId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(category => category.Name, category => category.Name), ct) != 1)
            {
                throw new ArgumentException("The selected category is unavailable.");
            }
            var platformIds = entry.SocialProfiles.Select(profile => profile.PlatformId).ToList();
            foreach (var platformId in platformIds.Order())
            {
                if (await context.SocialMediaDefaults.Where(platform => platform.Id == platformId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(platform => platform.Name, platform => platform.Name), ct) != 1)
                {
                    throw new ArgumentException("A selected social platform is unavailable.");
                }
            }
            var platforms = await context.SocialMediaDefaults.Where(platform => platformIds.Contains(platform.Id)).ToListAsync(ct);
            if (platforms.Count != platformIds.Count)
            {
                throw new ArgumentException("A selected social platform is unavailable.");
            }
            foreach (var profile in entry.SocialProfiles)
            {
                ValidateSocialHost(platforms.Single(platform => platform.Id == profile.PlatformId).Name, profile.Url);
            }
            var imageUrl = await images.SaveAsync(operation.Id, entry.ImageDataUrl, ct);
            operation.EntryJson = JsonSerializer.Serialize(new StoredEntry(entry.Name, entry.Username, entry.CategoryId,
                    entry.SocialProfiles, imageUrl));
        }
        else
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
            if (existing is null && entry is not null)
            {
                images.Delete(operation.Id);
            }
            if (entry is not null && await context.PaymentOperations.AnyAsync(payment => payment.ReservedUsername == entry.Username, ct))
            {
                throw new InvalidOperationException("This username has an active checkout. Resume it or wait for it to expire.");
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
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var claimed = await context.PaymentOperations.Where(operation => operation.Id == checkout.OperationId && operation.FulfilledAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.FulfilledAt, clock.GetUtcNow().UtcDateTime), ct);
        if (claimed == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        var payment = await context.PaymentOperations.SingleAsync(operation => operation.Id == checkout.OperationId, ct);
        var timestamp = payment.PaidAt!.Value;
        Guid entryId;
        if (payment.Purpose == PaymentPurpose.Entry)
        {
            var details = JsonSerializer.Deserialize<StoredEntry>(payment.EntryJson!)!;
            var category = await context.Categories.SingleAsync(category => category.Id == details.CategoryId, ct);
            var entry = new Entry(payment.Id)
            {
                Name = details.Name,
                Username = details.Username,
                ImgUrl = details.ImageUrl,
                Score = payment.Amount,
                CreatedDate = timestamp,
                Categories = [category]
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
        await transaction.CommitAsync(ct);
        context.ChangeTracker.Clear();
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
                && (operation.Status == PaymentStatus.Pending || operation.Status == PaymentStatus.Processing || operation.Status == PaymentStatus.Paid))
            .OrderBy(operation => operation.LastCheckedAt ?? operation.CreatedDate).Take(100).ToListAsync(ct);
        var ids = pending.Select(operation => operation.Id).ToList();
        await context.PaymentOperations.Where(operation => ids.Contains(operation.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.LastCheckedAt, clock.GetUtcNow().UtcDateTime), ct);
        return pending;
    }

    private static void ValidateSocialHost(string platform, string url)
    {
        var allowed = platform.ToLowerInvariant() switch
        {
        "instagram" => new[]
        {
            "instagram.com"
        }, "tiktok" => ["tiktok.com"],
        "youtube" => ["youtube.com", "youtu.be"], "onlyfans" => ["onlyfans.com"],
        "facebook" => ["facebook.com", "fb.com"], "x" or "twitter" => ["x.com", "twitter.com"],
        "twitch" => ["twitch.tv"], _ => []
        };
        var host = new Uri(url).Host;
        if (allowed.Length > 0 && !allowed.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A social URL does not match its selected platform.");
        }
    }

    private sealed record StoredEntry(string Name, string Username, Guid CategoryId, List<SocialProfileRequest> SocialProfiles, string ImageUrl);
}
