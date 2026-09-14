using System.Collections.Concurrent;
using System.Text.Json;
using CrownRank.Application.Abstractions;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Infrastructure.Payments;

// Development only. Test outcomes are stored on disk for console restart/reconciliation exercises.
public sealed class MockPaymentGateway : IPaymentGateway
{
    private sealed record StoredPurchase(long AmountInMinorUnits, string Currency, MockOutcome Outcome);
    private readonly ConcurrentDictionary<string, StoredPurchase> _purchases;
    private readonly object _sync = new();
    private readonly string? _stateFile;

    public enum MockOutcome { Processing, Succeeded, Failed, Cancelled }

    public MockPaymentGateway(string? stateFile = null)
    {
        _stateFile = stateFile;
        _purchases = stateFile is not null && File.Exists(stateFile)
            ? new ConcurrentDictionary<string, StoredPurchase>(JsonSerializer.Deserialize<Dictionary<string, StoredPurchase>>(
                File.ReadAllText(stateFile)) ?? [], StringComparer.Ordinal)
            : new ConcurrentDictionary<string, StoredPurchase>(StringComparer.Ordinal);
    }

    public void SetOutcome(Guid attemptId, MockOutcome outcome)
    {
        var reference = attemptId.ToString("N");
        lock (_sync)
        {
            if (!_purchases.TryGetValue(reference, out var stored)) throw new KeyNotFoundException("Unknown mock checkout.");
            _purchases[reference] = stored with { Outcome = outcome };
            Persist();
        }
    }

    public MockOutcome GetOutcome(Guid attemptId)
        => _purchases.TryGetValue(attemptId.ToString("N"), out var stored) ? stored.Outcome : MockOutcome.Processing;

    public Task<CheckoutSession> CreateCheckoutAsync(Guid attemptId, Money expected, CancellationToken ct = default)
    {
        if (attemptId == Guid.Empty) throw new ArgumentException("Attempt ID is required.", nameof(attemptId));
        ArgumentNullException.ThrowIfNull(expected);
        ct.ThrowIfCancellationRequested();
        var reference = attemptId.ToString("N");
        lock (_sync)
        {
            var candidate = new StoredPurchase(expected.AmountInMinorUnits, expected.Currency, MockOutcome.Processing);
            var stored = _purchases.GetOrAdd(reference, candidate);
            if (stored.AmountInMinorUnits != candidate.AmountInMinorUnits || stored.Currency != candidate.Currency)
                throw new InvalidOperationException("The attempt already has a different amount.");
            Persist();
        }
        return Task.FromResult(new CheckoutSession("mock", reference, $"mock://checkout/{reference}"));
    }

    public Task<VerifiedPayment> VerifyAsync(string provider, string reference, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (provider != "mock" || !_purchases.TryGetValue(reference, out var stored))
            throw new InvalidOperationException("Unknown mock checkout.");
        return Task.FromResult(new VerifiedPayment("mock", reference,
            Money.Create(stored.AmountInMinorUnits, stored.Currency), stored.Outcome == MockOutcome.Succeeded,
            stored.Outcome switch
            {
                MockOutcome.Failed => PaymentFailure.Failed,
                MockOutcome.Cancelled => PaymentFailure.Cancelled,
                _ => null
            }));
    }

    private void Persist()
    {
        if (_stateFile is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_stateFile))!);
        var temporary = _stateFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_purchases.ToDictionary(x => x.Key, x => x.Value)));
        File.Move(temporary, _stateFile, overwrite: true);
    }
}
