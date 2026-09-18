using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Application.Payments;
using CrownRank.Application.Services;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.UnitTest;

public sealed class EntrySubmissionServiceTests
{
    private readonly FixedClock clock = new();
    private readonly FakeStore store = new();
    private readonly FakeImages images = new();
    private readonly FakeGateway gateway = new();
    private readonly Category category;

    public EntrySubmissionServiceTests()
    {
        category = Category.Create("Technology", null, clock.UtcNow);
        store.Category = category;
    }

    private EntrySubmissionService Service
    {
        get
        {
            var payments = new PaymentService(store, store, store, gateway, store, clock);
            return new EntrySubmissionService(store, store, images, new FakeLegal(), store, payments, clock);
        }
    }

    [Fact]
    public async Task Valid_submission_creates_entry_and_checkout_in_one_transaction()
    {
        var result = await Service.SubmitAsync(Request());

        Assert.NotEqual(Guid.Empty, result.EntryId);
        Assert.NotEqual(Guid.Empty, result.AttemptId);
        Assert.Single(store.Entries);
        Assert.Single(store.Attempts);
        Assert.Equal(1, store.TransactionCount);
        Assert.Empty(images.DeletedKeys);
    }

    [Fact]
    public async Task Validation_failure_does_not_store_an_image()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.SubmitAsync(Request(acceptedAgreements: false)));

        Assert.Equal(0, images.SaveCount);
        Assert.Empty(store.Entries);
    }

    [Fact]
    public async Task Payment_setup_failure_rolls_back_and_deletes_the_image()
    {
        gateway.CreateException = new InvalidOperationException("Checkout unavailable.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SubmitAsync(Request()));

        Assert.Empty(store.Entries);
        Assert.Empty(store.Attempts);
        Assert.Single(images.DeletedKeys);
    }

    [Fact]
    public async Task Cancellation_rolls_back_and_cleanup_does_not_reuse_the_cancelled_token()
    {
        gateway.CreateException = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Service.SubmitAsync(Request()));

        Assert.Empty(store.Entries);
        Assert.Empty(store.Attempts);
        Assert.Single(images.DeletedKeys);
        Assert.False(images.DeleteToken.CanBeCanceled);
    }

    private SubmitEntryRequest Request(bool acceptedAgreements = true) => new(
        "Ada Lovelace",
        "ada",
        category.Id,
        new MemoryStream([1, 2, 3]),
        "profile.png",
        [new SocialLinkInput(SocialMediaPlatform.Instagram, "https://instagram.com/ada")],
        acceptedAgreements,
        1_250,
        "EUR");

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLegal : ILegalDocumentVersions
    {
        public Task<LegalVersions> CurrentAsync(CancellationToken ct = default)
            => Task.FromResult(new LegalVersions("terms-1", "privacy-1", "rules-1"));
    }

    private sealed class FakeImages : IProfileImageStorage
    {
        public int SaveCount { get; private set; }
        public List<string> DeletedKeys { get; } = [];
        public CancellationToken DeleteToken { get; private set; }

        public Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult("/uploads/profiles/00000000000000000000000000000001.png");
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            DeleteToken = ct;
            DeletedKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public Exception? CreateException { get; set; }

        public Task<CheckoutSession> CreateCheckoutAsync(
            Guid attemptId, Money expected, CancellationToken ct = default)
        {
            if (CreateException is not null) return Task.FromException<CheckoutSession>(CreateException);
            return Task.FromResult(new CheckoutSession(
                "mock", attemptId.ToString(), "mock://" + attemptId));
        }

        public Task<VerifiedPayment> VerifyAsync(
            string provider, string reference, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeStore :
        ICategoryRepository,
        IEntryRepository,
        IContributionRepository,
        IPaymentAttemptRepository,
        IUnitOfWork
    {
        public Category? Category { get; set; }
        public Dictionary<Guid, Entry> Entries { get; } = [];
        public Dictionary<Guid, PaymentAttempt> Attempts { get; } = [];
        public List<RankingContribution> Contributions { get; } = [];
        public int TransactionCount { get; private set; }

        public Task<Category?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Category?.Id == id ? Category : null);

        Task<Entry?> IEntryRepository.GetAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Entries.GetValueOrDefault(id));

        Task<PaymentAttempt?> IPaymentAttemptRepository.GetAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Attempts.GetValueOrDefault(id));

        public Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Category>>(Category is null ? [] : [Category]);

        public Task AddAsync(Category value, CancellationToken ct = default)
        {
            Category = value;
            return Task.CompletedTask;
        }

        public Task AddAsync(Entry value, CancellationToken ct = default)
        {
            Entries.Add(value.Id, value);
            return Task.CompletedTask;
        }

        public Task AddAsync(PaymentAttempt value, CancellationToken ct = default)
        {
            Attempts.Add(value.Id, value);
            return Task.CompletedTask;
        }

        public Task AddAsync(RankingContribution value, CancellationToken ct = default)
        {
            Contributions.Add(value);
            return Task.CompletedTask;
        }

        public Task<RankingContribution?> FindByPaymentAsync(
            string provider, string reference, CancellationToken ct = default)
            => Task.FromResult(Contributions.SingleOrDefault(
                x => x.PaymentProvider == provider && x.PaymentReference == reference));

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            TransactionCount++;
            var existingEntries = Entries.Keys.ToHashSet();
            var existingAttempts = Attempts.Keys.ToHashSet();
            var contributionCount = Contributions.Count;
            try
            {
                return await action(ct);
            }
            catch
            {
                foreach (var id in Entries.Keys.Where(id => !existingEntries.Contains(id)).ToArray())
                    Entries.Remove(id);
                foreach (var id in Attempts.Keys.Where(id => !existingAttempts.Contains(id)).ToArray())
                    Attempts.Remove(id);
                Contributions.RemoveRange(contributionCount, Contributions.Count - contributionCount);
                throw;
            }
        }
    }
}
