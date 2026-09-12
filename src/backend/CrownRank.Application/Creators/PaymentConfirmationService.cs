using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class PaymentConfirmationService(
    ICreatorRepository repository,
    TimeProvider timeProvider)
{
    public async Task ConfirmAsync(PaymentConfirmation confirmation, CancellationToken cancellationToken)
    {
        Money.Validate(confirmation.Amount);
        if (!string.Equals(confirmation.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only USD is supported.");
        if (confirmation.ReferenceId == Guid.Empty || string.IsNullOrWhiteSpace(confirmation.PaymentReference))
            throw new ArgumentException("The payment confirmation is missing its reference.");

        var kind = confirmation.Purpose switch
        {
            "ranking-entry" => ContributionKind.RankUp,
            "creator-boost" => ContributionKind.Boost,
            _ => throw new ArgumentException("Unknown payment purpose.")
        };
        var existing = await repository.GetContributionAsync(confirmation.PaymentReference, cancellationToken);
        if (existing is not null)
        {
            if (existing.CreatorId != confirmation.CreatorId || existing.Amount != confirmation.Amount || existing.Kind != kind)
                throw new InvalidOperationException("The payment reference was already used for different details.");
            return;
        }

        var creator = kind == ContributionKind.RankUp
            ? await repository.GetByEntryReferenceAsync(confirmation.ReferenceId, cancellationToken)
            : await repository.GetByIdAsync(confirmation.CreatorId, cancellationToken);
        if (creator is null || creator.Id != confirmation.CreatorId || creator.IsHidden)
            throw new KeyNotFoundException("Creator not found.");
        if (kind == ContributionKind.RankUp && creator.OpeningAmount != confirmation.Amount)
            throw new InvalidOperationException("The confirmed amount does not match the pending entry.");
        if (kind == ContributionKind.Boost && creator.Contributions.Count == 0)
            throw new InvalidOperationException("Only published creators can receive a Boost.");

        creator.AddContribution(confirmation.Amount, kind, timeProvider.GetUtcNow(), confirmation.PaymentReference);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
