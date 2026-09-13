using CrownRank.Domain.Common;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Models
{
    public sealed class RankingContribution : Entity
    {
        public Guid EntryId { get; private set; }

        public ContributionType Type { get; private set; }

        public Money Amount { get; private set; } = null!;

        public string PaymentProvider { get; private set; } =
            string.Empty;

        public string PaymentReference { get; private set; } =
            string.Empty;

        public ContributionStatus Status { get; private set; }

        public DateTimeOffset ConfirmedAtUtc { get; private set; }

        public ContributionExclusionReason? ExclusionReason
        {
            get;
            private set;
        }

        public DateTimeOffset? ExcludedAtUtc
        {
            get;
            private set;
        }

        private RankingContribution()
        {
        }

        private RankingContribution(
            Guid entryId,
            ContributionType type,
            Money amount,
            string paymentProvider,
            string paymentReference,
            DateTimeOffset confirmedAtUtc)
        {
            EntryId = entryId;
            Type = type;
            Amount = amount;

            PaymentProvider =
                paymentProvider.Trim().ToLowerInvariant();

            PaymentReference =
                paymentReference.Trim();

            Status = ContributionStatus.Active;

            ConfirmedAtUtc = confirmedAtUtc;
        }

        public static RankingContribution CreateInitialPayment(
            Entry entry,
            Money amount,
            string paymentProvider,
            string paymentReference,
            DateTimeOffset confirmedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (entry.Status != EntryStatus.PendingPayment)
            {
                throw new DomainException(
                    "Initial payment can only be applied to a pending entry.");
            }

            Validate(
                amount,
                paymentProvider,
                paymentReference);

            return new RankingContribution(
                entry.Id,
                ContributionType.InitialEntryPayment,
                amount,
                paymentProvider,
                paymentReference,
                confirmedAtUtc);
        }

        public static RankingContribution CreateBoost(
            Entry entry,
            Money amount,
            string paymentProvider,
            string paymentReference,
            DateTimeOffset confirmedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (entry.Status != EntryStatus.Published)
            {
                throw new DomainException(
                    "Only published entries can receive boosts.");
            }

            Validate(
                amount,
                paymentProvider,
                paymentReference);

            return new RankingContribution(
                entry.Id,
                ContributionType.Boost,
                amount,
                paymentProvider,
                paymentReference,
                confirmedAtUtc);
        }

        public void ExcludeFromRanking(
            ContributionExclusionReason reason,
            DateTimeOffset excludedAtUtc)
        {
            if (!Enum.IsDefined(
                    typeof(ContributionExclusionReason),
                    reason))
            {
                throw new DomainException(
                    "The contribution exclusion reason is invalid.");
            }

            if (Status == ContributionStatus.Excluded)
            {
                return;
            }

            Status = ContributionStatus.Excluded;
            ExclusionReason = reason;
            ExcludedAtUtc = excludedAtUtc;
        }

        private static void Validate(
            Money amount,
            string paymentProvider,
            string paymentReference)
        {
            ArgumentNullException.ThrowIfNull(amount);

            if (amount.AmountInMinorUnits <= 0)
            {
                throw new DomainException(
                    "Contribution amount must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(
                    paymentProvider))
            {
                throw new DomainException(
                    "Payment provider is required.");
            }

            if (paymentProvider.Trim().Length > 50)
            {
                throw new DomainException(
                    "Payment provider cannot exceed 50 characters.");
            }

            if (string.IsNullOrWhiteSpace(
                    paymentReference))
            {
                throw new DomainException(
                    "Payment reference is required.");
            }

            if (paymentReference.Trim().Length > 255)
            {
                throw new DomainException(
                    "Payment reference cannot exceed 255 characters.");
            }
        }
    }
}
