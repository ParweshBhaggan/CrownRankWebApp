using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class CreatorServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Entry_is_saved_and_mock_checkout_is_confirmed()
    {
        var repository = new MemoryRepository();
        var gateway = new StubGateway();
        var service = Service(repository, gateway: gateway);

        var result = await service.StartEntryCheckoutAsync(TestData.Command(username: "@Ada"), Ct);

        Assert.True(result.Session.Confirmed);
        Assert.Equal(repository.Items.Single().Id, result.CreatorId);
        Assert.Equal("ada", repository.Items.Single().Username);
        Assert.Equal(12.50m, repository.Items.Single().Contributions.Sum(x => x.Amount));
        Assert.Single(repository.Items.Single().SocialProfiles);
        Assert.Single(repository.Items);
        Assert.Equal(2, repository.SaveCalls);
        var checkout = Assert.Single(gateway.Requests);
        Assert.Equal("ranking-entry", checkout.Purpose);
        Assert.Equal("USD", checkout.Currency);
    }

    [Fact]
    public async Task Exact_retry_returns_same_creator_and_never_double_credits()
    {
        var repository = new MemoryRepository();
        var gateway = new StubGateway();
        var service = Service(repository, gateway: gateway);
        var command = TestData.Command();

        var first = await service.StartEntryCheckoutAsync(command, Ct);
        var retry = await service.StartEntryCheckoutAsync(command, Ct);

        Assert.Equal(first.CreatorId, retry.CreatorId);
        Assert.Single(repository.Items.Single().Contributions);
        Assert.Single(gateway.Requests);
        Assert.Equal(2, repository.SaveCalls);
    }

    [Fact]
    public async Task Retry_with_changed_details_is_rejected()
    {
        var repository = new MemoryRepository();
        var service = Service(repository);
        var command = TestData.Command();
        await service.StartEntryCheckoutAsync(command, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartEntryCheckoutAsync(command with { InitialAmount = 99m }, Ct));
    }

    [Fact]
    public async Task Existing_ranked_username_is_rejected()
    {
        var creator = TestData.Creator();
        creator.PrepareEntry(Guid.NewGuid(), 10m);
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "opening");
        var repository = new MemoryRepository();
        repository.Items.Add(creator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(repository).StartEntryCheckoutAsync(TestData.Command(username: "ADA"), Ct));
    }

    [Fact]
    public async Task Matching_pending_creator_is_recovered_after_an_interrupted_old_flow()
    {
        var pending = TestData.Creator();
        pending.PrepareEntry(Guid.NewGuid(), 12.50m);
        var repository = new MemoryRepository();
        repository.Items.Add(pending);
        var command = TestData.Command();

        var recovered = await Service(repository).StartEntryCheckoutAsync(command, Ct);

        Assert.Equal(pending.Id, recovered.CreatorId);
        Assert.Equal(command.EntryReference, pending.EntryReference);
        Assert.Single(pending.Contributions);
    }

    [Fact]
    public async Task Unconfirmed_checkout_keeps_creator_pending_and_hidden()
    {
        var repository = new MemoryRepository();
        var result = await Service(repository, gateway: new StubGateway(false)).StartEntryCheckoutAsync(TestData.Command(), Ct);
        Assert.False(result.Session.Confirmed);
        Assert.Single(repository.Items);
        Assert.Empty(repository.Items.Single().Contributions);
        Assert.Empty(await Service(repository).GetAllAsync(Ct));
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Stored_image_is_removed_when_entry_persistence_fails()
    {
        var repository = new MemoryRepository { SaveFailure = new InvalidOperationException("database failed") };
        var images = new RecordingImages();
        await using var stream = new MemoryStream([1, 2, 3]);
        var command = TestData.Command() with
        {
            Image = stream, ImageFileName = "avatar.png", ImageContentType = "image/png", ImageLength = 3
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(repository, images).StartEntryCheckoutAsync(command, Ct));

        Assert.Equal(1, images.SaveCalls);
        Assert.Equal(new[] { "avatar.webp" }, images.DeletedKeys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Entry_requires_between_one_and_five_social_profiles(int count)
    {
        var profiles = Enumerable.Range(0, count)
            .Select(index => new SocialProfileInput(SocialPlatform.Website, $"https://example.com/{index}"))
            .ToList();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new MemoryRepository()).StartEntryCheckoutAsync(TestData.Command() with { SocialProfiles = profiles }, Ct));
    }

    [Fact]
    public async Task Rankings_use_score_then_reached_time_and_utc_day()
    {
        var later = TestData.Creator("later");
        var earlier = TestData.Creator("earlier");
        later.AddContribution(20m, ContributionKind.RankUp, TestData.Now.AddHours(-1), "later-opening");
        earlier.AddContribution(20m, ContributionKind.RankUp, TestData.Now.AddHours(-2), "earlier-opening");
        later.AddContribution(5m, ContributionKind.Boost, new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero), "tomorrow");
        var repository = new MemoryRepository();
        repository.Items.AddRange([later, earlier]);
        var service = Service(repository);

        Assert.Equal(new[] { later.Id, earlier.Id }, (await service.GetAllAsync(Ct)).Select(x => x.Id));
        Assert.Equal(new[] { earlier.Id, later.Id }, (await service.GetDailyAsync(new(2026, 9, 8), Ct)).Select(x => x.Id));
        Assert.Equal(later.Id, Assert.Single(await service.GetDailyAsync(new(2026, 9, 9), Ct)).Id);
    }

    [Fact]
    public async Task Delete_hides_profile_but_retains_ledger()
    {
        var creator = TestData.Creator();
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "opening");
        var repository = new MemoryRepository();
        repository.Items.Add(creator);
        var service = Service(repository);

        Assert.True(await service.DeleteAsync(creator.Id, Ct));
        Assert.Null(await service.GetAsync(creator.Id, Ct));
        Assert.Empty(await service.GetAllAsync(Ct));
        Assert.Single(creator.Contributions);
        Assert.False(await service.DeleteAsync(Guid.NewGuid(), Ct));
    }

    private static CreatorService Service(MemoryRepository repository, RecordingImages? images = null, StubGateway? gateway = null) =>
        new(repository, images ?? new RecordingImages(), gateway ?? new StubGateway(), new FixedClock());
}
