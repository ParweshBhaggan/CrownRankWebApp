using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class MockCheckoutService(ICreatorRepository repository, IPaymentGateway gateway, TimeProvider clock)
{
    public async Task<CheckoutSession> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        Money.Validate(request.Amount);
        if (request.ReferenceId == Guid.Empty) throw new ArgumentException("A checkout reference is required.");
        var kind = request.Purpose switch { "ranking-entry" => ContributionKind.RankUp, "creator-boost" => ContributionKind.Boost, _ => throw new ArgumentException("Unknown checkout purpose.") };
        if (request.Currency != "USD") throw new ArgumentException("Only USD is supported.");
        var creator = await repository.GetForUpdateAsync(request.CreatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Creator not found.");
        var reference = $"mock-{request.ReferenceId:N}";
        var existing = await repository.GetContributionAsync(reference, cancellationToken);
        if (existing is not null)
        {
            if (existing.CreatorId != request.CreatorId || existing.Amount != request.Amount || existing.Kind != kind)
                throw new InvalidOperationException("This checkout reference was already used for a different payment.");
            return new CheckoutSession(reference, true);
        }
        if (creator.IsHidden) throw new KeyNotFoundException("Creator not found.");
        if (kind == ContributionKind.RankUp && (creator.Contributions.Count > 0 || creator.OpeningAmount != request.Amount))
            throw new InvalidOperationException("The entry is already confirmed or the opening amount does not match.");
        if (kind == ContributionKind.Boost && creator.Contributions.Count == 0)
            throw new InvalidOperationException("Only published creators can receive a Boost.");
        var session = await gateway.CreateCheckoutAsync(request, cancellationToken);
        if (!session.Confirmed) return session;
        creator.AddContribution(request.Amount, kind, clock.GetUtcNow(), reference);
        await repository.SaveChangesAsync(cancellationToken);
        return new CheckoutSession(reference, true);
    }
}
