using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Application.Payments;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.Services;

public sealed class PaymentService(
    IEntryRepository entries, IContributionRepository contributions, IPaymentAttemptRepository attempts,
    IPaymentGateway gateway, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<PaymentStart> StartBoostAsync(Guid entryId, Money amount, CancellationToken ct = default)
    {
        var entry = await entries.GetAsync(entryId, ct) ?? throw new KeyNotFoundException("Entry not found.");
        if (entry.Status != EntryStatus.Published) throw new InvalidOperationException("Only published entries can be boosted.");
        return await StartAsync(entry.Id, amount, PaymentPurpose.Boost, ct);
    }

    public async Task<PaymentStart> StartAsync(Guid entryId, Money amount, PaymentPurpose purpose, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        var entry = await entries.GetAsync(entryId, ct) ?? throw new KeyNotFoundException("Entry not found.");
        if (purpose == PaymentPurpose.InitialEntry && entry.Status != EntryStatus.PendingPayment ||
            purpose == PaymentPurpose.Boost && entry.Status != EntryStatus.Published)
            throw new InvalidOperationException("Entry is not eligible for this payment.");
        var attempt = new PaymentAttempt(entryId, amount, purpose, clock.UtcNow);
        await attempts.AddAsync(attempt, ct);
        await unitOfWork.SaveAsync(ct);
        var session = await gateway.CreateCheckoutAsync(attempt.Id, amount, ct);
        attempt.SetCheckout(session);
        await unitOfWork.SaveAsync(ct);
        return new PaymentStart(attempt.Id, entryId, session.CheckoutUrl);
    }

    public async Task<RankingContribution?> ConfirmAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await attempts.GetAsync(attemptId, ct) ?? throw new KeyNotFoundException("Payment attempt not found.");
        if (attempt.State == PaymentState.Confirmed)
            return await contributions.FindByPaymentAsync(attempt.Provider!, attempt.Reference!, ct);
        if (attempt.State != PaymentState.CheckoutReady) throw new InvalidOperationException("Checkout is not ready.");
        var verified = await gateway.VerifyAsync(attempt.Provider!, attempt.Reference!, ct);
        if (!verified.Succeeded) return null;
        if (!string.Equals(verified.Provider, attempt.Provider, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(verified.Reference, attempt.Reference, StringComparison.Ordinal) ||
            verified.Amount != attempt.ExpectedAmount)
            throw new InvalidOperationException("Verified payment does not match the expected purchase.");

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Re-read under a transaction lock; Infrastructure must serialize confirmation for this attempt.
            var current = await attempts.GetAsync(attemptId, token) ?? throw new KeyNotFoundException("Payment attempt not found.");
            var existing = await contributions.FindByPaymentAsync(current.Provider!, current.Reference!, token);
            if (current.State == PaymentState.Confirmed) return existing;
            if (current.State != PaymentState.CheckoutReady || existing is not null)
                throw new InvalidOperationException("Payment reference has already been used.");
            var entry = await entries.GetAsync(current.EntryId, token) ?? throw new KeyNotFoundException("Entry not found.");
            var contribution = current.Purpose == PaymentPurpose.InitialEntry
                ? RankingContribution.CreateInitialPayment(entry, current.ExpectedAmount, current.Provider!, current.Reference!, clock.UtcNow)
                : RankingContribution.CreateBoost(entry, current.ExpectedAmount, current.Provider!, current.Reference!, clock.UtcNow);
            if (current.Purpose == PaymentPurpose.InitialEntry) entry.Publish(contribution);
            await contributions.AddAsync(contribution, token);
            current.Confirm(clock.UtcNow);
            await unitOfWork.SaveAsync(token);
            return contribution;
        }, ct);
    }
}
