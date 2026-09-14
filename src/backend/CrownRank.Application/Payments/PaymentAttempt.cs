using CrownRank.Application.Abstractions;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.Payments;

public enum PaymentPurpose { InitialEntry, Boost }
public enum PaymentState { Pending, CheckoutReady, Confirmed, Failed }

public sealed class PaymentAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EntryId { get; private set; }
    public Money ExpectedAmount { get; private set; } = null!;
    public PaymentPurpose Purpose { get; private set; }
    public PaymentState State { get; private set; }
    public string? Provider { get; private set; }
    public string? Reference { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    private PaymentAttempt() { }
    public PaymentAttempt(Guid entryId, Money expectedAmount, PaymentPurpose purpose, DateTimeOffset createdAtUtc)
    {
        if (entryId == Guid.Empty) throw new ArgumentException("Entry ID is required.", nameof(entryId));
        ArgumentNullException.ThrowIfNull(expectedAmount);
        if (expectedAmount.AmountInMinorUnits <= 0) throw new ArgumentException("Payment amount must be positive.", nameof(expectedAmount));
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        EntryId = entryId;
        ExpectedAmount = expectedAmount;
        Purpose = purpose;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetCheckout(CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (State != PaymentState.Pending || string.IsNullOrWhiteSpace(session.Provider) ||
            string.IsNullOrWhiteSpace(session.Reference) || string.IsNullOrWhiteSpace(session.CheckoutUrl))
            throw new InvalidOperationException("A valid checkout is required for a pending attempt.");
        Provider = session.Provider.Trim().ToLowerInvariant();
        Reference = session.Reference.Trim();
        CheckoutUrl = session.CheckoutUrl;
        State = PaymentState.CheckoutReady;
    }

    public void Confirm(DateTimeOffset now)
    {
        if (State != PaymentState.CheckoutReady) throw new InvalidOperationException("Checkout is not ready.");
        State = PaymentState.Confirmed;
        ConfirmedAtUtc = now;
    }
}
