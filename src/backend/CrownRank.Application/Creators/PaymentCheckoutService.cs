using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class PaymentCheckoutService(ICreatorRepository repository, IPaymentGateway gateway, TimeProvider clock)
{
    public async Task<CheckoutSession> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        Money.Validate(request.Amount);
        if (request.ReferenceId == Guid.Empty) throw new ArgumentException("A checkout reference is required.");
        if (request.Purpose != "creator-boost") throw new ArgumentException("Only creator Boosts use this checkout endpoint.");
        var kind = ContributionKind.Boost;
        if (request.Currency != "USD") throw new ArgumentException("Only USD is supported.");
        var creator = await repository.GetByIdAsync(request.CreatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Creator not found.");
        var mockReference = $"mock-{request.ReferenceId:N}";
        var stripeReference = $"stripe-{request.ReferenceId:N}";
        var existing = await repository.GetContributionAsync(mockReference, cancellationToken)
            ?? await repository.GetContributionAsync(stripeReference, cancellationToken);
        if (existing is not null)
        {
            if (existing.CreatorId != request.CreatorId || existing.Amount != request.Amount || existing.Kind != kind)
                throw new InvalidOperationException("This checkout reference was already used for a different payment.");
            return new CheckoutSession(existing.PaymentReference, true);
        }
        if (creator.IsHidden) throw new KeyNotFoundException("Creator not found.");
        if (creator.Contributions.Count == 0)
            throw new InvalidOperationException("Only published creators can receive a Boost.");
        var session = await gateway.CreateCheckoutAsync(request, cancellationToken);
        if (!session.Confirmed) return session;
        creator.AddContribution(request.Amount, kind, clock.GetUtcNow(), session.Id);
        await repository.SaveChangesAsync(cancellationToken);
        return session;
    }
}
