using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain
{
    public static class TestData
    {
        public static readonly DateTimeOffset CreatedAtUtc =
            new(
                2026,
                9,
                14,
                10,
                0,
                0,
                TimeSpan.Zero);

        public static Category CreateCategory(
            string name = "Creators")
        {
            return Category.Create(
                name,
                "Test category",
                CreatedAtUtc);
        }

        public static AgreementAcceptance CreateAgreement()
        {
            return AgreementAcceptance.Create(
                "terms-v1",
                "privacy-v1",
                "rules-v1",
                CreatedAtUtc);
        }

        public static SocialMediaLink CreateInstagramLink(
            string username = "alice")
        {
            return SocialMediaLink.Create(
                SocialMediaPlatform.Instagram,
                $"https://instagram.com/{username}");
        }

        public static Entry CreatePendingEntry(
            Category? category = null,
            AgreementAcceptance? agreement = null,
            IEnumerable<SocialMediaLink>? links = null)
        {
            category ??= CreateCategory();
            agreement ??= CreateAgreement();

            links ??=
            [
                CreateInstagramLink()
            ];

            return Entry.Create(
                "Alice",
                "alice",
                category,
                "profiles/alice.webp",
                agreement,
                links,
                CreatedAtUtc);
        }

        public static RankingContribution CreateInitialContribution(
            Entry entry,
            string reference = "mock_initial_001")
        {
            return RankingContribution.CreateInitialPayment(
                entry,
                Money.Create(2500, "EUR"),
                "mock",
                reference,
                CreatedAtUtc.AddMinutes(1));
        }

        public static (
            Entry Entry,
            RankingContribution InitialContribution)
            CreatePublishedEntry()
        {
            var entry =
                CreatePendingEntry();

            var contribution =
                CreateInitialContribution(entry);

            entry.Publish(contribution);

            return (
                entry,
                contribution);
        }
    }
}
