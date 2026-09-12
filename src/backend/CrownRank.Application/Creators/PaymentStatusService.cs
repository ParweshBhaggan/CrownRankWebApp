using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class PaymentStatusService(ICreatorRepository repository)
{
    public async Task<PaymentStatus> GetAsync(
        Guid referenceId,
        Guid creatorId,
        string purpose,
        CancellationToken cancellationToken)
    {
        if (referenceId == Guid.Empty || creatorId == Guid.Empty)
            throw new ArgumentException("Payment and creator references are required.");
        var expectedKind = purpose switch
        {
            "ranking-entry" => ContributionKind.RankUp,
            "creator-boost" => ContributionKind.Boost,
            _ => throw new ArgumentException("Unknown payment purpose.")
        };

        var stripe = await repository.GetContributionAsync($"stripe-{referenceId:N}", cancellationToken);
        var mock = stripe is null
            ? await repository.GetContributionAsync($"mock-{referenceId:N}", cancellationToken)
            : null;
        var contribution = stripe ?? mock;
        return new PaymentStatus(creatorId, contribution?.CreatorId == creatorId && contribution.Kind == expectedKind);
    }
}
