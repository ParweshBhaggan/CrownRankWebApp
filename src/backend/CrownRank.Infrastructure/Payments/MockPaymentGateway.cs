using System.Collections.Concurrent;
using CrownRank.Application.Abstractions;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Infrastructure.Payments;

// Development only. Simulates successful checkout deterministically; never register in production.
public sealed class MockPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, Money> _purchases = new(StringComparer.Ordinal);

    public Task<CheckoutSession> CreateCheckoutAsync(Guid attemptId, Money expected, CancellationToken ct = default)
    {
        if (attemptId == Guid.Empty) throw new ArgumentException("Attempt ID is required.", nameof(attemptId));
        ArgumentNullException.ThrowIfNull(expected);
        var reference = attemptId.ToString("N");
        var stored = _purchases.GetOrAdd(reference, expected);
        if (stored != expected) throw new InvalidOperationException("The attempt already has a different amount.");
        return Task.FromResult(new CheckoutSession("mock", reference, $"mock://checkout/{reference}"));
    }

    public Task<VerifiedPayment> VerifyAsync(string provider, string reference, CancellationToken ct = default)
    {
        if (provider != "mock" || !_purchases.TryGetValue(reference, out var amount))
            throw new InvalidOperationException("Unknown mock checkout.");
        return Task.FromResult(new VerifiedPayment("mock", reference, amount, true));
    }
}
