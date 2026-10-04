using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class PaymentRecoverySettings
{
    public bool Enabled
    {
        get;
        set;
    } = true;

    public int PollSeconds
    {
        get;
        set;
    } = 60;

    public int RetryMinutes
    {
        get;
        set;
    } = 5;

    public int StaleEntryMinutes
    {
        get;
        set;
    } = 30;

    public int BatchSize
    {
        get;
        set;
    } = 20;
}

public sealed class PaymentReconciler(ApplicationDbContext context, IPaymentService payments, TimeProvider clock, PaymentRecoverySettings settings, ILogger<PaymentReconciler> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = now.AddMinutes(-settings.RetryMinutes);
        var stale = now.AddMinutes(-settings.StaleEntryMinutes);
        var recent = now.AddDays(-7);
        var candidates = await context.CheckoutPayments.AsNoTracking()
            .Where(payment => payment.FulfilledEntryId == null && (payment.LastCheckedAt == null || payment.LastCheckedAt < due)
                && ((payment.PaidAt != null && (payment.Purpose == "boost" || payment.PaidAt < stale))
                || (payment.SessionId != null && payment.PaidAt == null && payment.ExpiresAt > recent)))
            .OrderBy(payment => payment.LastCheckedAt).ThenBy(payment => payment.CreatedAt)
            .Take(settings.BatchSize).ToListAsync(ct);
        foreach (var payment in candidates)
        {
            // Atomic claim prevents multiple instances from polling the same receipt at once.
            var claimed = await context.CheckoutPayments.Where(value => value.Id == payment.Id && value.FulfilledEntryId == null
                    && (value.LastCheckedAt == null || value.LastCheckedAt < due))
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.LastCheckedAt, now), ct);
            if (claimed != 1)
            {
                continue;
            }
            try
            {
                if (payment.PaidAt.HasValue && payment.Purpose == "entry")
                {
                    logger.LogWarning("Paid entry {PaymentId} remains unregistered since {PaidAt}; review or recover the paid form.", payment.Id, payment.PaidAt);
                    continue;
                }
                if (payment.SessionId is null)
                {
                    logger.LogError("Paid operation {PaymentId} has no Stripe session reference; manual review is required.", payment.Id);
                    continue;
                }
                var result = await payments.ConfirmAsync(payment.Id, ct);
                if (result.Fulfilled)
                {
                    logger.LogInformation("Reconciled payment {PaymentId} for {Purpose}.", payment.Id, result.Purpose);
                }
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                // The persisted cooldown keeps failures retryable without hammering Stripe.
                // Never log forms, keys, names, or Stripe response bodies.
                logger.LogError("Reconciliation for payment {PaymentId} failed with {ErrorType}; it remains retryable.", payment.Id, exception.GetType().Name);
            }
        }
    }
}
