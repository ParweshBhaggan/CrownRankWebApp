using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class BehaviorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Money_RejectsFractionalCentsAndOutOfRangeAmounts()
    {
        foreach (var amount in new[] { 0m, -1m, 1.001m, 10_000.01m }) Assert.Throws<ArgumentException>(() => Money.Validate(amount));
        Money.Validate(12.50m);
    }

    [Fact]
    public async Task Entry_RemainsPrivateUntilMockConfirmation_AndRetryCreditsOnce()
    {
        var repository = new MemoryRepository();
        var creators = new CreatorService(repository, new NoImages(), new Clock());
        var reference = Guid.NewGuid();
        var command = new CreateCreatorCommand("Ada", "Lovelace", "ada", CreatorCategory.Technology,
            reference, [new(SocialPlatform.Instagram, "https://instagram.com/ada")], 12.50m, null, null, null, 0);
        var entry = await creators.CreateAsync(command, Ct);
        Assert.Empty(await creators.GetAllAsync(Ct));
        Assert.Equal(entry.Id, (await creators.CreateAsync(command, Ct)).Id);
        var checkout = new MockCheckoutService(repository, new MockGateway(), new Clock());
        var request = new CheckoutRequest(entry.Id, 12.50m, "USD", "ranking-entry", Guid.NewGuid());
        await checkout.CheckoutAsync(request, Ct);
        await checkout.CheckoutAsync(request, Ct);
        var published = Assert.Single(await creators.GetAllAsync(Ct));
        Assert.Equal(12.50m, published.TotalContributed);
        Assert.Single(repository.Items.Single().Contributions);
    }

    [Fact]
    public async Task Boost_PersistsOnce_AndRejectsReusingAReferenceForAnotherAmount()
    {
        var creator = NewCreator(); creator.AddContribution(10m, ContributionKind.RankUp, Now, "initial");
        var repository = new MemoryRepository(); repository.Items.Add(creator);
        var service = new MockCheckoutService(repository, new MockGateway(), new Clock());
        var request = new CheckoutRequest(creator.Id, 2.50m, "USD", "creator-boost", Guid.NewGuid());
        await service.CheckoutAsync(request, Ct); await service.CheckoutAsync(request, Ct);
        Assert.Equal(12.50m, creator.Contributions.Sum(x => x.Amount));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckoutAsync(request with { Amount = 3m }, Ct));
    }

    [Fact]
    public async Task FailedMock_DoesNotPublishOrCredit()
    {
        var creator = NewCreator(); creator.PrepareEntry(Guid.NewGuid(), 10m);
        var repository = new MemoryRepository(); repository.Items.Add(creator);
        var service = new MockCheckoutService(repository, new MockGateway(false), new Clock());
        var result = await service.CheckoutAsync(new(creator.Id, 10m, "USD", "ranking-entry", Guid.NewGuid()), Ct);
        Assert.False(result.Confirmed); Assert.Empty(creator.Contributions);
    }

    [Fact]
    public async Task DailyRanking_UsesUtcBoundariesAndScoreReachedTime()
    {
        var first = NewCreator(); var second = NewCreator();
        first.AddContribution(20m, ContributionKind.RankUp, Now.AddHours(-1), "first");
        second.AddContribution(20m, ContributionKind.RankUp, Now.AddHours(-2), "second");
        first.AddContribution(5m, ContributionKind.Boost, new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero), "tomorrow");
        var repository = new MemoryRepository(); repository.Items.AddRange([first, second]);
        var service = new CreatorService(repository, new NoImages(), new Clock());
        var daily = await service.GetDailyAsync(new(2026, 9, 8), Ct);
        Assert.Equal(second.Id, daily[0].Id); Assert.Equal(20m, daily[1].TotalContributed);
        Assert.Single(await service.GetDailyAsync(new(2026, 9, 9), Ct));
        await service.DeleteAsync(first.Id, Ct);
        Assert.Equal(2, first.Contributions.Count);
        Assert.Single(await service.GetAllAsync(Ct));
    }

    [Fact]
    public void SocialLinks_MustMatchTheSelectedPlatform()
    {
        Assert.Throws<ArgumentException>(() => NewCreator().AddSocialProfile(SocialPlatform.Instagram, "https://instagram.com.evil.example/person"));
        NewCreator().AddSocialProfile(SocialPlatform.Instagram, "https://www.instagram.com/person");
    }

    private static Creator NewCreator() => new(Guid.NewGuid(), "Ada", "Lovelace", "ada", CreatorCategory.Technology, "/avatar.svg", null, Now.AddDays(-10));
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MockGateway(bool confirmed = true) : IPaymentGateway
    {
        public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken) => Task.FromResult(new CheckoutSession("mock", confirmed));
    }
    private sealed class NoImages : IProfileImageService
    {
        public Task<StoredProfileImage> SaveAsync(ProfileImageUpload upload, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class MemoryRepository : ICreatorRepository
    {
        public List<Creator> Items { get; } = [];
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Creator>>(Items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
        public Task<Creator?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => GetByIdAsync(id, cancellationToken);
        public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) => Task.FromResult(Items.Any(x => x.Username == username));
        public Task<Creator?> GetByEntryReferenceAsync(Guid reference, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(x => x.EntryReference == reference));
        public Task<Contribution?> GetContributionAsync(string reference, CancellationToken cancellationToken) => Task.FromResult(Items.SelectMany(x => x.Contributions).SingleOrDefault(x => x.PaymentReference == reference));
        public Task AddAsync(Creator creator, CancellationToken cancellationToken) { Items.Add(creator); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
