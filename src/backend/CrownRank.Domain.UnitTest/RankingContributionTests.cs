using CrownRank.Domain.Common;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class RankingContributionTests
    {
        [Fact]
        public void CreateInitialPayment_ForPendingEntry_CreatesActiveContribution()
        {
            var entry =
                TestData.CreatePendingEntry();

            var confirmedAt =
                TestData.CreatedAtUtc.AddMinutes(5);

            var contribution =
                RankingContribution.CreateInitialPayment(
                    entry,
                    Money.Create(2500, "EUR"),
                    " Stripe ",
                    " pi_123 ",
                    confirmedAt);

            Assert.Equal(
                entry.Id,
                contribution.EntryId);

            Assert.Equal(
                ContributionType.InitialEntryPayment,
                contribution.Type);

            Assert.Equal(
                2500,
                contribution.Amount.AmountInMinorUnits);

            Assert.Equal(
                "stripe",
                contribution.PaymentProvider);

            Assert.Equal(
                "pi_123",
                contribution.PaymentReference);

            Assert.Equal(
                ContributionStatus.Active,
                contribution.Status);

            Assert.Equal(
                confirmedAt,
                contribution.ConfirmedAtUtc);

            Assert.Null(
                contribution.ExclusionReason);

            Assert.Null(
                contribution.ExcludedAtUtc);
        }

        [Fact]
        public void CreateInitialPayment_WhenEntryAlreadyPublished_Throws()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateInitialPayment(
                        entry,
                        Money.Create(1000, "EUR"),
                        "mock",
                        "mock_second_initial",
                        TestData.CreatedAtUtc.AddHours(1)));
        }

        [Fact]
        public void CreateBoost_ForPublishedEntry_CreatesBoost()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            var contribution =
                RankingContribution.CreateBoost(
                    entry,
                    Money.Create(1000, "EUR"),
                    "mock",
                    "mock_boost_001",
                    TestData.CreatedAtUtc.AddHours(1));

            Assert.Equal(
                ContributionType.Boost,
                contribution.Type);

            Assert.Equal(
                entry.Id,
                contribution.EntryId);

            Assert.Equal(
                ContributionStatus.Active,
                contribution.Status);
        }

        [Fact]
        public void CreateBoost_ForPendingEntry_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateBoost(
                        entry,
                        Money.Create(1000, "EUR"),
                        "mock",
                        "mock_boost_001",
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void CreateBoost_ForHiddenEntry_Throws()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            entry.Hide(
                TestData.CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateBoost(
                        entry,
                        Money.Create(1000, "EUR"),
                        "mock",
                        "mock_boost_001",
                        TestData.CreatedAtUtc.AddHours(2)));
        }

        [Fact]
        public void CreateContribution_WithZeroAmount_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateInitialPayment(
                        entry,
                        Money.Zero("EUR"),
                        "mock",
                        "mock_initial",
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void CreateContribution_WithoutProvider_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateInitialPayment(
                        entry,
                        Money.Create(1000, "EUR"),
                        " ",
                        "mock_initial",
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void CreateContribution_WithoutPaymentReference_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            Assert.Throws<DomainException>(
                () =>
                    RankingContribution.CreateInitialPayment(
                        entry,
                        Money.Create(1000, "EUR"),
                        "mock",
                        " ",
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void ExcludeFromRanking_ExcludesContribution()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            var excludedAt =
                TestData.CreatedAtUtc.AddHours(1);

            contribution.ExcludeFromRanking(
                ContributionExclusionReason.PaymentDisputed,
                excludedAt);

            Assert.Equal(
                ContributionStatus.Excluded,
                contribution.Status);

            Assert.Equal(
                ContributionExclusionReason.PaymentDisputed,
                contribution.ExclusionReason);

            Assert.Equal(
                excludedAt,
                contribution.ExcludedAtUtc);
        }

        [Fact]
        public void ExcludeFromRanking_WhenAlreadyExcluded_IsIdempotent()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            var firstTime =
                TestData.CreatedAtUtc.AddHours(1);

            contribution.ExcludeFromRanking(
                ContributionExclusionReason.PaymentDisputed,
                firstTime);

            contribution.ExcludeFromRanking(
                ContributionExclusionReason.FraudulentPayment,
                TestData.CreatedAtUtc.AddHours(2));

            Assert.Equal(
                ContributionExclusionReason.PaymentDisputed,
                contribution.ExclusionReason);

            Assert.Equal(
                firstTime,
                contribution.ExcludedAtUtc);
        }

        [Fact]
        public void ExcludeFromRanking_WithInvalidReason_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            var invalidReason =
                (ContributionExclusionReason)999;

            Assert.Throws<DomainException>(
                () =>
                    contribution.ExcludeFromRanking(
                        invalidReason,
                        TestData.CreatedAtUtc));
        }
    }
}
