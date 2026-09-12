using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;

namespace CrownRank.ArchitectureTests;

internal static class TestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    internal static Creator Creator(string username = "ada", DateTimeOffset? createdAt = null) =>
        new(Guid.NewGuid(), "Ada Lovelace", username, CreatorCategory.Technology,
            "/avatar.svg", null, createdAt ?? Now.AddDays(-10));

    internal static CreateCreatorCommand Command(Guid? reference = null, string username = "ada", decimal amount = 12.50m) =>
        new("Ada Lovelace", username, CreatorCategory.Technology, reference ?? Guid.NewGuid(),
            [new(SocialPlatform.Instagram, "https://instagram.com/ada")], amount, null, null, null, 0);
}

internal sealed class FixedClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = TestData.Now;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class StubGateway(bool confirmed = true) : IPaymentGateway
{
    public bool Confirmed { get; set; } = confirmed;
    public Exception? Failure { get; set; }
    public List<CheckoutRequest> Requests { get; } = [];

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (Failure is not null) throw Failure;
        return Task.FromResult(new CheckoutSession($"mock-{request.ReferenceId:N}", Confirmed));
    }
}

internal sealed class RecordingImages : IProfileImageService
{
    public StoredProfileImage Stored { get; set; } = new("/uploads/avatar.webp", "avatar.webp");
    public int SaveCalls { get; private set; }
    public List<string> DeletedKeys { get; } = [];

    public Task<StoredProfileImage> SaveAsync(ProfileImageUpload upload, CancellationToken cancellationToken)
    {
        SaveCalls++;
        return Task.FromResult(Stored);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        DeletedKeys.Add(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class MemoryRepository : ICreatorRepository
{
    public List<Creator> Items { get; } = [];
    public int SaveCalls { get; private set; }
    public Exception? SaveFailure { get; set; }

    public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Creator>>(Items);

    public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(x => x.Id == id));

    public Task<Creator?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(x => x.Username == username));

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.Username == username));

    public Task<Creator?> GetByEntryReferenceAsync(Guid reference, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(x => x.EntryReference == reference));

    public Task<Contribution?> GetContributionAsync(string reference, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SelectMany(x => x.Contributions).SingleOrDefault(x => x.PaymentReference == reference));

    public Task AddAsync(Creator creator, CancellationToken cancellationToken)
    {
        Items.Add(creator);
        return Task.CompletedTask;
    }

    public void Remove(Creator creator) => Items.Remove(creator);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return SaveFailure is null ? Task.CompletedTask : Task.FromException(SaveFailure);
    }
}

internal sealed class MemoryPendingEntries : IPendingRankingEntryRepository
{
    public List<PendingRankingEntry> Items { get; } = [];
    public int SaveCalls { get; private set; }
    public Exception? SaveFailure { get; set; }

    public Task<PendingRankingEntry?> GetByReferenceAsync(Guid referenceId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(x => x.ReferenceId == referenceId));

    public Task<PendingRankingEntry?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(x => x.Username == username));

    public Task AddAsync(PendingRankingEntry entry, CancellationToken cancellationToken)
    {
        Items.Add(entry);
        return Task.CompletedTask;
    }

    public void Remove(PendingRankingEntry entry) => Items.Remove(entry);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return SaveFailure is null ? Task.CompletedTask : Task.FromException(SaveFailure);
    }
}
