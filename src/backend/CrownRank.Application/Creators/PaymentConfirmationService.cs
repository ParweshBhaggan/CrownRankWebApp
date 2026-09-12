using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed class PaymentConfirmationService(
    ICreatorRepository repository,
    IPendingRankingEntryRepository pendingEntries,
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

        if (kind == ContributionKind.RankUp)
        {
            var pending = await pendingEntries.GetByReferenceAsync(confirmation.ReferenceId, cancellationToken)
                ?? throw new KeyNotFoundException("Pending ranking entry not found.");
            if (pending.CreatorId != confirmation.CreatorId || pending.Amount != confirmation.Amount)
                throw new InvalidOperationException("The confirmation does not match the pending ranking entry.");
            if (await repository.UsernameExistsAsync(pending.Username, cancellationToken))
                throw new InvalidOperationException("That username is already ranked.");

            var creator = pending.Confirm(timeProvider.GetUtcNow(), confirmation.PaymentReference);
            await repository.AddAsync(creator, cancellationToken);
            pendingEntries.Remove(pending);
            await repository.SaveChangesAsync(cancellationToken);
            return;
        }

        var boostedCreator = await repository.GetByIdAsync(confirmation.CreatorId, cancellationToken);
        if (boostedCreator is null || boostedCreator.IsHidden)
            throw new KeyNotFoundException("Creator not found.");
        if (boostedCreator.Contributions.Count == 0)
            throw new InvalidOperationException("Only published creators can receive a Boost.");

        boostedCreator.AddContribution(confirmation.Amount, kind, timeProvider.GetUtcNow(), confirmation.PaymentReference);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
