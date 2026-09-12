using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class PaymentConfirmationServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Signed_entry_confirmation_publishes_pending_creator_once()
    {
        var reference = Guid.NewGuid();
        var creator = TestData.Creator();
        creator.PrepareEntry(reference, 12.50m);
        var repository = new MemoryRepository();
        repository.Items.Add(creator);
        var service = new PaymentConfirmationService(repository, new FixedClock());
        var confirmation = new PaymentConfirmation(
            creator.Id, 12.50m, "USD", "ranking-entry", reference, $"stripe-{reference:N}");

        await service.ConfirmAsync(confirmation, Ct);
        await service.ConfirmAsync(confirmation, Ct);

        var contribution = Assert.Single(creator.Contributions);
        Assert.Equal(ContributionKind.RankUp, contribution.Kind);
        Assert.Equal(12.50m, contribution.Amount);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Signed_boost_confirmation_credits_published_creator_once()
    {
        var reference = Guid.NewGuid();
        var creator = TestData.Creator();
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "opening");
        var repository = new MemoryRepository();
        repository.Items.Add(creator);
        var service = new PaymentConfirmationService(repository, new FixedClock());

        await service.ConfirmAsync(new PaymentConfirmation(
            creator.Id, 2.50m, "USD", "creator-boost", reference, $"stripe-{reference:N}"), Ct);

        Assert.Equal(12.50m, creator.Contributions.Sum(x => x.Amount));
        Assert.Equal(ContributionKind.Boost, creator.Contributions.Last().Kind);
    }

    [Fact]
    public async Task Confirmation_rejects_tampered_or_unpublishable_details()
    {
        var reference = Guid.NewGuid();
        var pending = TestData.Creator();
        pending.PrepareEntry(reference, 12.50m);
        var repository = new MemoryRepository();
        repository.Items.Add(pending);
        var service = new PaymentConfirmationService(repository, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(new PaymentConfirmation(
            pending.Id, 99m, "USD", "ranking-entry", reference, $"stripe-{reference:N}"), Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(new PaymentConfirmation(
            pending.Id, 2.50m, "USD", "creator-boost", Guid.NewGuid(), "stripe-boost"), Ct));
        Assert.Empty(pending.Contributions);
    }

    [Fact]
    public async Task Payment_status_recognizes_confirmed_provider_reference()
    {
        var reference = Guid.NewGuid();
        var creator = TestData.Creator();
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, $"stripe-{reference:N}");
        var repository = new MemoryRepository();
        repository.Items.Add(creator);
        var service = new PaymentStatusService(repository);

        Assert.True((await service.GetAsync(reference, creator.Id, "ranking-entry", Ct)).Confirmed);
        Assert.False((await service.GetAsync(Guid.NewGuid(), creator.Id, "creator-boost", Ct)).Confirmed);
    }
}
