using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class PaymentCheckoutServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Confirmed_boost_is_persisted_once_across_retries()
    {
        var (service, creator, repository, gateway) = PublishedCreator();
        var request = Request(creator.Id);

        var first = await service.CheckoutAsync(request, Ct);
        var retry = await service.CheckoutAsync(request, Ct);

        Assert.True(first.Confirmed);
        Assert.Equal(first, retry);
        Assert.Equal(12.50m, creator.Contributions.Sum(x => x.Amount));
        Assert.Single(gateway.Requests);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Reference_cannot_be_reused_for_different_payment_details()
    {
        var (service, creator, _, _) = PublishedCreator();
        var request = Request(creator.Id);
        await service.CheckoutAsync(request, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckoutAsync(request with { Amount = 3m }, Ct));
    }

    [Theory]
    [InlineData("EUR", "creator-boost")]
    [InlineData("USD", "ranking-entry")]
    public async Task Checkout_rejects_unsupported_currency_or_purpose(string currency, string purpose)
    {
        var (service, creator, _, _) = PublishedCreator();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CheckoutAsync(Request(creator.Id) with { Currency = currency, Purpose = purpose }, Ct));
    }

    [Fact]
    public async Task Checkout_rejects_missing_hidden_or_unpublished_creator()
    {
        var repository = new MemoryRepository();
        var service = new PaymentCheckoutService(repository, new StubGateway(), new FixedClock());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CheckoutAsync(Request(Guid.NewGuid()), Ct));

        var unpublished = TestData.Creator("pending");
        repository.Items.Add(unpublished);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckoutAsync(Request(unpublished.Id), Ct));

        var hidden = TestData.Creator("hidden");
        hidden.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "hidden-opening");
        hidden.Hide();
        repository.Items.Add(hidden);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CheckoutAsync(Request(hidden.Id), Ct));
    }

    [Fact]
    public async Task Unconfirmed_mock_result_does_not_credit_or_save()
    {
        var (service, creator, repository, _) = PublishedCreator(confirmed: false);
        var result = await service.CheckoutAsync(Request(creator.Id), Ct);
        Assert.False(result.Confirmed);
        Assert.Single(creator.Contributions);
        Assert.Equal(0, repository.SaveCalls);
    }

    private static CheckoutRequest Request(Guid creatorId) =>
        new(creatorId, 2.50m, "USD", "creator-boost", Guid.NewGuid());

    private static (PaymentCheckoutService Service, Creator Creator, MemoryRepository Repository, StubGateway Gateway)
        PublishedCreator(bool confirmed = true)
    {
        var creator = TestData.Creator();
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "opening");
        var repository = new MemoryRepository();
        repository.Items.Add(creator);
        var gateway = new StubGateway(confirmed);
        return (new PaymentCheckoutService(repository, gateway, new FixedClock()), creator, repository, gateway);
    }
}
