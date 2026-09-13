using CrownRank.Application.Abstractions;
using CrownRank.Application.Payments;
using CrownRank.Application.Services;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.UnitTest;

public sealed class PaymentServiceTests
{
    private readonly FakeStore store = new();
    private readonly FakeGateway gateway = new();
    private readonly FixedClock clock = new();
    private PaymentService Service => new(store, store, store, gateway, store, clock);

    [Fact]
    public async Task Confirmation_rejects_a_successful_payment_with_wrong_amount()
    {
        var attempt = await StartInitialAsync();
        gateway.VerifiedAmount = Money.Create(999, "EUR");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.ConfirmAsync(attempt.Id));
        Assert.Empty(store.Contributions);
        Assert.Equal(PaymentState.CheckoutReady, attempt.State);
        Assert.Equal(EntryStatus.PendingPayment, store.Entries[attempt.EntryId].Status);
    }

    [Fact]
    public async Task Repeated_confirmation_publishes_once()
    {
        var attempt = await StartInitialAsync();
        var first = await Service.ConfirmAsync(attempt.Id);
        var second = await Service.ConfirmAsync(attempt.Id);
        Assert.NotNull(first);
        Assert.Equal(first.Id, second?.Id);
        Assert.Single(store.Contributions);
        Assert.Equal(EntryStatus.Published, store.Entries[attempt.EntryId].Status);
    }

    private async Task<PaymentAttempt> StartInitialAsync()
    {
        var category = Category.Create("Creators", null, clock.UtcNow);
        var entry = Entry.Create("Name", "handle", category, "image-key",
            AgreementAcceptance.Create("1", "1", "1", clock.UtcNow),
            [SocialMediaLink.Create(SocialMediaPlatform.Instagram, "https://instagram.com/example")], clock.UtcNow);
        store.Entries[entry.Id] = entry;
        var result = await Service.StartAsync(entry.Id, Money.Create(1000, "EUR"), PaymentPurpose.InitialEntry);
        return store.Attempts[result.AttemptId];
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero); }
    private sealed class FakeGateway : IPaymentGateway
    {
        public Money? VerifiedAmount { get; set; }
        public Task<CheckoutSession> CreateCheckoutAsync(Guid id, Money amount, CancellationToken ct = default)
            => Task.FromResult(new CheckoutSession("mock", id.ToString(), "https://checkout.example/" + id));
        public Task<VerifiedPayment> VerifyAsync(string provider, string reference, CancellationToken ct = default)
            => Task.FromResult(new VerifiedPayment(provider, reference, VerifiedAmount ?? Money.Create(1000, "EUR"), true));
    }
    private sealed class FakeStore : IEntryRepository, IContributionRepository, IPaymentAttemptRepository, IUnitOfWork
    {
        public Dictionary<Guid, Entry> Entries { get; } = [];
        public Dictionary<Guid, PaymentAttempt> Attempts { get; } = [];
        public List<RankingContribution> Contributions { get; } = [];
        public Task<Entry?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Entries.GetValueOrDefault(id));
        Task<PaymentAttempt?> IPaymentAttemptRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Attempts.GetValueOrDefault(id));
        public Task AddAsync(Entry entry, CancellationToken ct = default) { Entries.Add(entry.Id, entry); return Task.CompletedTask; }
        public Task AddAsync(PaymentAttempt attempt, CancellationToken ct = default) { Attempts.Add(attempt.Id, attempt); return Task.CompletedTask; }
        public Task AddAsync(RankingContribution contribution, CancellationToken ct = default) { Contributions.Add(contribution); return Task.CompletedTask; }
        public Task<RankingContribution?> FindByPaymentAsync(string provider, string reference, CancellationToken ct = default)
            => Task.FromResult(Contributions.SingleOrDefault(x => x.PaymentProvider == provider && x.PaymentReference == reference));
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct);
    }
}
