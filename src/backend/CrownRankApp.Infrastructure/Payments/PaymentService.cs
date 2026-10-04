using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Payments;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class PaymentService(ApplicationDbContext context, IEntryServices entries, IPaymentGateway gateway, PaymentSettings settings, StripeSettings stripe, TimeProvider clock) : IPaymentService
{
    public async Task<CheckoutResponse> StartAsync(Guid referenceId, CheckoutRequest request, Guid? boostEntryId, CancellationToken ct)
    {
        if (referenceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            throw new ArgumentException("A checkout reference and name up to 100 characters are required.");
        }
        if (request.Amount < settings.MinimumAmount || request.Amount > settings.MaximumAmount || decimal.Round(request.Amount, 2) != request.Amount)
        {
            throw new ArgumentException($"Choose {settings.Currency.ToUpperInvariant()} {settings.MinimumAmount:0.00}–{settings.MaximumAmount:0.00} with at most two decimal places.");
        }
        var payment = await context.CheckoutPayments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == referenceId, ct);
        if (payment is null)
        {
            if (boostEntryId.HasValue && !await context.Entries.AnyAsync(entry => entry.Id == boostEntryId, ct))
            {
                throw new KeyNotFoundException("The creator is unavailable.");
            }
            var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds()).UtcDateTime;
            payment = new CheckoutPayment
            {
                Id = referenceId,
                Name = request.Name.Trim(),
                Amount = request.Amount,
                Currency = settings.Currency,
                Purpose = boostEntryId.HasValue ? "boost" : "entry",
                BoostEntryId = boostEntryId,
                CreatedAt = now,
                ExpiresAt = now.AddHours(24),
                FrontendUrl = stripe.FrontendUrl.TrimEnd('/'),
                LiveMode = stripe.LiveMode
            };
            context.CheckoutPayments.Add(payment);
            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
                payment = await context.CheckoutPayments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == referenceId, ct);
                if (payment is null)
                {
                    throw;
                }
            }
            context.ChangeTracker.Clear();
        }
        if (payment.Name != request.Name.Trim() || payment.Amount != request.Amount || payment.BoostEntryId != boostEntryId)
        {
            throw new InvalidOperationException("This checkout reference already has different details. Resume the existing checkout.");
        }
        var session = await GetSessionAsync(payment, ct);
        if (session is null)
        {
            return new CheckoutResponse(payment.Id, null, "expired");
        }
        var response = await ApplyAsync(payment, session, ct);
        return new CheckoutResponse(payment.Id, response.Status == "pending" ? session.Url : null, response.Status);
    }

    public async Task<CheckoutResponse> ResumeAsync(Guid referenceId, CancellationToken ct)
    {
        var payment = await FindAsync(referenceId, ct);
        return await StartAsync(referenceId, new CheckoutRequest(payment.Name, payment.Amount), payment.BoostEntryId, ct);
    }

    public async Task<PaymentResponse> ConfirmAsync(Guid referenceId, CancellationToken ct)
    {
        var payment = await FindAsync(referenceId, ct);
        if (payment.FulfilledEntryId.HasValue)
        {
            return ToResponse(payment, "paid");
        }
        var session = await GetSessionAsync(payment, ct);
        if (session is null)
        {
            return ToResponse(payment, "expired");
        }
        return await ApplyAsync(payment, session, ct);
    }

    public async Task<EntryResponseDto> RegisterEntryAsync(Guid referenceId, EntryResponseDto entry, CancellationToken ct)
    {
        var confirmed = await ConfirmAsync(referenceId, ct);
        if (confirmed.Purpose != "entry" || confirmed.Status != "paid")
        {
            throw new InvalidOperationException("A successful entry payment is required before registration.");
        }
        // Lock this payment while the original service creates the entry. The entry and
        // consumed payment commit together, so concurrent return-page retries publish once.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.CheckoutPayments.Where(value => value.Id == referenceId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Amount, value => value.Amount), ct);
        var payment = await context.CheckoutPayments.SingleAsync(value => value.Id == referenceId, ct);
        if (payment.FulfilledEntryId.HasValue)
        {
            await transaction.CommitAsync(ct);
            return await GetEntryAsync(payment.FulfilledEntryId.Value);
        }
        if (entry.Name.Trim() != payment.Name)
        {
            throw new ArgumentException("The entry name must match the paid checkout.");
        }
        // Preserve the existing DTO, image and CreateAsync implementation; only the
        // opening score comes from Stripe's verified amount instead of the browser.
        entry.Score = payment.Amount;
        try
        {
            var created = await entries.CreateAsync(entry);
            payment.FulfilledEntryId = created.Id;
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return EntryResponseDto.FromEntry(created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            if (await context.Entries.AnyAsync(value => value.Username == entry.Username.Trim(), ct))
            {
                throw new InvalidOperationException("An entry with this username already exists. Your payment is saved; choose another username and retry.");
            }
            throw;
        }
    }

    public async Task<EntryResponseDto> RegisterBoostAsync(Guid referenceId, Guid entryId, CancellationToken ct)
    {
        var payment = await FindAsync(referenceId, ct);
        if (payment.Purpose != "boost" || payment.BoostEntryId != entryId)
        {
            throw new InvalidOperationException("This payment belongs to another operation.");
        }
        var confirmed = await ConfirmAsync(referenceId, ct);
        if (!confirmed.Fulfilled)
        {
            throw new InvalidOperationException("The boost has not been paid yet.");
        }
        return await GetEntryAsync(entryId);
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken ct)
    {
        var notification = gateway.VerifyWebhook(payload, signature);
        if (notification is null)
        {
            return;
        }
        var payment = await context.CheckoutPayments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == notification.ReferenceId, ct);
        if (payment is null)
        {
            return;
        }
        // Retrieve current Stripe state, so a delayed expired/failed event cannot undo payment.
        var session = await gateway.RetrieveAsync(notification.SessionId, ct);
        await ApplyAsync(payment, session, ct);
    }

    private async Task<VerifiedCheckout?> GetSessionAsync(CheckoutPayment payment, CancellationToken ct)
    {
        if (payment.SessionId is not null)
        {
            return await gateway.RetrieveAsync(payment.SessionId, ct);
        }
        var checkoutContext = new CheckoutContext(payment.Id, payment.Currency, payment.CreatedAt, payment.ExpiresAt, payment.FrontendUrl);
        // Stripe requires at least 30 minutes until expiry. Never create a new session
        // after the old idempotency key could be pruned; recover a lost response instead.
        if (clock.GetUtcNow().UtcDateTime < payment.ExpiresAt.AddMinutes(-31))
        {
            return await gateway.CreateAsync(new CheckoutRequest(payment.Name, payment.Amount), checkoutContext, ct);
        }
        return await gateway.FindAsync(checkoutContext, ct);
    }

    private async Task<PaymentResponse> ApplyAsync(CheckoutPayment payment, VerifiedCheckout session, CancellationToken ct)
    {
        if (session.ReferenceId != payment.Id || session.AmountInCents != checked((long)(payment.Amount * 100))
            || session.Currency != payment.Currency || session.LiveMode != payment.LiveMode
            || (payment.SessionId is not null && session.SessionId != payment.SessionId))
        {
            throw new InvalidOperationException("Stripe payment details do not match this checkout.");
        }
        await context.CheckoutPayments.Where(value => value.Id == payment.Id && value.SessionId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.SessionId, session.SessionId), ct);
        payment = await FindAsync(payment.Id, ct);
        if (payment.SessionId != session.SessionId)
        {
            throw new InvalidOperationException("This checkout is linked to another Stripe session.");
        }
        if (session.Paid)
        {
            var paidAt = session.PaidAt ?? clock.GetUtcNow().UtcDateTime;
            await context.CheckoutPayments.Where(value => value.Id == payment.Id && value.PaidAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.PaidAt, paidAt), ct);
            payment.PaidAt ??= paidAt;
        }
        if (payment.PaidAt.HasValue && payment.Purpose == "boost" && !payment.FulfilledEntryId.HasValue)
        {
            // Reuse the existing boost's transaction and unique score-addition reference.
            // If receipt saving fails, a retry observes that addition instead of crediting twice.
            var boosted = await entries.BoostScoreAsync(payment.BoostEntryId!.Value, payment.Amount, payment.Id);
            if (boosted is null)
            {
                throw new InvalidOperationException("Payment received, but the creator is unavailable. Contact support with this payment reference.");
            }
            await context.CheckoutPayments.Where(value => value.Id == payment.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.FulfilledEntryId, boosted.Id), ct);
            payment.FulfilledEntryId = boosted.Id;
        }
        var status = payment.PaidAt.HasValue ? "paid" : session.Status switch
        {
        "expired" => "expired",
        "complete" => "processing",
        _ => "pending"
        };
        return ToResponse(payment, status);
    }

    private async Task<CheckoutPayment> FindAsync(Guid id, CancellationToken ct)
    {
        var payment = await context.CheckoutPayments.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, ct);
        if (payment is null)
        {
            throw new KeyNotFoundException("Payment reference not found.");
        }
        return payment;
    }

    private async Task<EntryResponseDto> GetEntryAsync(Guid id)
    {
        var entry = await entries.GetByIdAsync(id);
        if (entry is null)
        {
            throw new KeyNotFoundException("The registered creator is unavailable.");
        }
        return EntryResponseDto.FromEntry(entry);
    }

    private static PaymentResponse ToResponse(CheckoutPayment payment, string status)
    {
        return new PaymentResponse(payment.Id, status, payment.FulfilledEntryId.HasValue, payment.FulfilledEntryId, payment.Amount, payment.Currency, payment.Purpose, payment.Name);
    }
}
