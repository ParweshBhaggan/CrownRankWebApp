using CrownRank.Domain.Common;
using CrownRank.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class EntryTests
    {
        [Fact]
        public void Create_WithValidData_CreatesPendingEntry()
        {
            var category =
                TestData.CreateCategory();

            var agreement =
                TestData.CreateAgreement();

            var link =
                TestData.CreateInstagramLink();

            var entry =
                Entry.Create(
                    " Alice ",
                    " alice ",
                    category,
                    " profiles/alice.webp ",
                    agreement,
                    [link],
                    TestData.CreatedAtUtc);

            Assert.Equal(
                "Alice",
                entry.Name);

            Assert.Equal(
                "alice",
                entry.Username);

            Assert.Equal(
                category.Id,
                entry.CategoryId);

            Assert.Equal(
                "profiles/alice.webp",
                entry.ProfileImageKey);

            Assert.Equal(
                EntryStatus.PendingPayment,
                entry.Status);

            Assert.Null(
                entry.InitialContributionId);

            Assert.Null(
                entry.PublishedAtUtc);

            Assert.Single(
                entry.SocialMediaLinks);

            Assert.Equal(
                TestData.CreatedAtUtc,
                entry.CreatedAtUtc);

            Assert.Equal(
                TestData.CreatedAtUtc,
                entry.UpdatedAtUtc);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithoutName_Throws(
            string name)
        {
            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        name,
                        "alice",
                        TestData.CreateCategory(),
                        "profiles/alice.webp",
                        TestData.CreateAgreement(),
                        [TestData.CreateInstagramLink()],
                        TestData.CreatedAtUtc));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithoutUsername_Throws(
            string username)
        {
            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        "Alice",
                        username,
                        TestData.CreateCategory(),
                        "profiles/alice.webp",
                        TestData.CreateAgreement(),
                        [TestData.CreateInstagramLink()],
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void Create_WithoutProfileImage_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        "Alice",
                        "alice",
                        TestData.CreateCategory(),
                        " ",
                        TestData.CreateAgreement(),
                        [TestData.CreateInstagramLink()],
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void Create_WithoutSocialMediaLinks_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        "Alice",
                        "alice",
                        TestData.CreateCategory(),
                        "profiles/alice.webp",
                        TestData.CreateAgreement(),
                        [],
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void Create_InArchivedCategory_Throws()
        {
            var category =
                TestData.CreateCategory();

            category.Archive(
                TestData.CreatedAtUtc.AddMinutes(1));

            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        "Alice",
                        "alice",
                        category,
                        "profiles/alice.webp",
                        TestData.CreateAgreement(),
                        [TestData.CreateInstagramLink()],
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void Create_WithDuplicateUrls_Throws()
        {
            var links =
                new[]
                {
                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/Alice"),

                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/alice")
                };

            Assert.Throws<DomainException>(
                () =>
                    Entry.Create(
                        "Alice",
                        "alice",
                        TestData.CreateCategory(),
                        "profiles/alice.webp",
                        TestData.CreateAgreement(),
                        links,
                        TestData.CreatedAtUtc));
        }

        [Fact]
        public void Create_WithMultipleLinksForSamePlatform_IsAllowed()
        {
            var links =
                new[]
                {
                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/alice"),

                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/alice.second"),

                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/alice.third")
                };

            var entry =
                Entry.Create(
                    "Alice",
                    "alice",
                    TestData.CreateCategory(),
                    "profiles/alice.webp",
                    TestData.CreateAgreement(),
                    links,
                    TestData.CreatedAtUtc);

            Assert.Equal(
                3,
                entry.SocialMediaLinks.Count);
        }

        [Fact]
        public void Create_WithManySocialMediaLinks_IsAllowed()
        {
            var links =
                Enumerable
                    .Range(1, 100)
                    .Select(index =>
                        SocialMediaLink.Create(
                            SocialMediaPlatform.Website,
                            $"https://example.com/profile/{index}"))
                    .ToList();

            var entry =
                Entry.Create(
                    "Alice",
                    "alice",
                    TestData.CreateCategory(),
                    "profiles/alice.webp",
                    TestData.CreateAgreement(),
                    links,
                    TestData.CreatedAtUtc);

            Assert.Equal(
                100,
                entry.SocialMediaLinks.Count);
        }

        [Fact]
        public void Publish_WithActiveInitialContribution_PublishesEntry()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            entry.Publish(
                contribution);

            Assert.Equal(
                EntryStatus.Published,
                entry.Status);

            Assert.Equal(
                contribution.Id,
                entry.InitialContributionId);

            Assert.Equal(
                contribution.ConfirmedAtUtc,
                entry.PublishedAtUtc);

            Assert.Equal(
                contribution.ConfirmedAtUtc,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void Publish_WithSameContributionTwice_IsIdempotent()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            entry.Publish(
                contribution);

            entry.Publish(
                contribution);

            Assert.Equal(
                EntryStatus.Published,
                entry.Status);

            Assert.Equal(
                contribution.Id,
                entry.InitialContributionId);
        }

        [Fact]
        public void Publish_WithDifferentInitialContributionAfterPublishing_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            var first =
                TestData.CreateInitialContribution(
                    entry,
                    "mock_initial_001");

            var second =
                TestData.CreateInitialContribution(
                    entry,
                    "mock_initial_002");

            entry.Publish(
                first);

            Assert.Throws<DomainException>(
                () =>
                    entry.Publish(second));
        }

        [Fact]
        public void Publish_WithContributionBelongingToAnotherEntry_Throws()
        {
            var firstEntry =
                TestData.CreatePendingEntry();

            var secondEntry =
                Entry.Create(
                    "Bob",
                    "bob",
                    TestData.CreateCategory(),
                    "profiles/bob.webp",
                    TestData.CreateAgreement(),
                    [
                        SocialMediaLink.Create(
                        SocialMediaPlatform.TikTok,
                        "https://tiktok.com/@bob")
                    ],
                    TestData.CreatedAtUtc);

            var contribution =
                TestData.CreateInitialContribution(
                    secondEntry);

            Assert.Throws<DomainException>(
                () =>
                    firstEntry.Publish(
                        contribution));
        }

        [Fact]
        public void Publish_WithExcludedInitialContribution_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            var contribution =
                TestData.CreateInitialContribution(
                    entry);

            contribution.ExcludeFromRanking(
                ContributionExclusionReason.PaymentReversed,
                TestData.CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    entry.Publish(
                        contribution));
        }

        [Fact]
        public void UpdateBasicDetails_UpdatesEntry()
        {
            var entry =
                TestData.CreatePendingEntry();

            var updatedAt =
                TestData.CreatedAtUtc.AddHours(1);

            entry.UpdateBasicDetails(
                " New Name ",
                " newusername ",
                updatedAt);

            Assert.Equal(
                "New Name",
                entry.Name);

            Assert.Equal(
                "newusername",
                entry.Username);

            Assert.Equal(
                updatedAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void ChangeCategory_ToActiveCategory_ChangesCategory()
        {
            var entry =
                TestData.CreatePendingEntry();

            var newCategory =
                Category.Create(
                    "Gaming",
                    null,
                    TestData.CreatedAtUtc);

            var changedAt =
                TestData.CreatedAtUtc.AddHours(1);

            entry.ChangeCategory(
                newCategory,
                changedAt);

            Assert.Equal(
                newCategory.Id,
                entry.CategoryId);

            Assert.Equal(
                changedAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void ChangeCategory_ToArchivedCategory_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            var category =
                Category.Create(
                    "Gaming",
                    null,
                    TestData.CreatedAtUtc);

            category.Archive(
                TestData.CreatedAtUtc.AddMinutes(1));

            Assert.Throws<DomainException>(
                () =>
                    entry.ChangeCategory(
                        category,
                        TestData.CreatedAtUtc.AddHours(1)));
        }

        [Fact]
        public void ReplaceSocialMediaLinks_ReplacesLinks()
        {
            var entry =
                TestData.CreatePendingEntry();

            var updatedAt =
                TestData.CreatedAtUtc.AddHours(1);

            var newLinks =
                new[]
                {
                SocialMediaLink.Create(
                    SocialMediaPlatform.YouTube,
                    "https://youtube.com/@alice"),

                SocialMediaLink.Create(
                    SocialMediaPlatform.Twitch,
                    "https://twitch.tv/alice")
                };

            entry.ReplaceSocialMediaLinks(
                newLinks,
                updatedAt);

            Assert.Equal(
                2,
                entry.SocialMediaLinks.Count);

            Assert.Contains(
                entry.SocialMediaLinks,
                link =>
                    link.Platform ==
                    SocialMediaPlatform.YouTube);

            Assert.Contains(
                entry.SocialMediaLinks,
                link =>
                    link.Platform ==
                    SocialMediaPlatform.Twitch);

            Assert.Equal(
                updatedAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void UpdateProfileImage_UpdatesImage()
        {
            var entry =
                TestData.CreatePendingEntry();

            var updatedAt =
                TestData.CreatedAtUtc.AddHours(1);

            entry.UpdateProfileImage(
                "profiles/new-image.webp",
                updatedAt);

            Assert.Equal(
                "profiles/new-image.webp",
                entry.ProfileImageKey);

            Assert.Equal(
                updatedAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void Hide_PublishedEntry_HidesEntry()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            var hiddenAt =
                TestData.CreatedAtUtc.AddHours(2);

            entry.Hide(
                hiddenAt);

            Assert.Equal(
                EntryStatus.Hidden,
                entry.Status);

            Assert.Equal(
                hiddenAt,
                entry.HiddenAtUtc);

            Assert.Equal(
                hiddenAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void Hide_PendingEntry_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            Assert.Throws<DomainException>(
                () =>
                    entry.Hide(
                        TestData.CreatedAtUtc.AddHours(1)));
        }

        [Fact]
        public void Restore_HiddenEntry_RestoresEntry()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            entry.Hide(
                TestData.CreatedAtUtc.AddHours(1));

            var restoredAt =
                TestData.CreatedAtUtc.AddHours(2);

            entry.Restore(
                restoredAt);

            Assert.Equal(
                EntryStatus.Published,
                entry.Status);

            Assert.Null(
                entry.HiddenAtUtc);

            Assert.Equal(
                restoredAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void Restore_WhenNotHidden_Throws()
        {
            var (
                entry,
                _) =
                TestData.CreatePublishedEntry();

            Assert.Throws<DomainException>(
                () =>
                    entry.Restore(
                        TestData.CreatedAtUtc.AddHours(1)));
        }

        [Fact]
        public void Archive_ArchivesEntry()
        {
            var entry =
                TestData.CreatePendingEntry();

            var archivedAt =
                TestData.CreatedAtUtc.AddHours(1);

            entry.Archive(
                archivedAt);

            Assert.Equal(
                EntryStatus.Archived,
                entry.Status);

            Assert.Equal(
                archivedAt,
                entry.ArchivedAtUtc);

            Assert.Equal(
                archivedAt,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void Archive_WhenAlreadyArchived_IsIdempotent()
        {
            var entry =
                TestData.CreatePendingEntry();

            var firstTime =
                TestData.CreatedAtUtc.AddHours(1);

            entry.Archive(
                firstTime);

            entry.Archive(
                TestData.CreatedAtUtc.AddHours(2));

            Assert.Equal(
                firstTime,
                entry.ArchivedAtUtc);

            Assert.Equal(
                firstTime,
                entry.UpdatedAtUtc);
        }

        [Fact]
        public void UpdateBasicDetails_WhenArchived_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            entry.Archive(
                TestData.CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    entry.UpdateBasicDetails(
                        "Changed",
                        "changed",
                        TestData.CreatedAtUtc.AddHours(2)));
        }

        [Fact]
        public void ReplaceSocialMediaLinks_WhenArchived_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            entry.Archive(
                TestData.CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    entry.ReplaceSocialMediaLinks(
                        [
                            SocialMediaLink.Create(
                            SocialMediaPlatform.YouTube,
                            "https://youtube.com/@alice")
                        ],
                        TestData.CreatedAtUtc.AddHours(2)));
        }

        [Fact]
        public void UpdateProfileImage_WhenArchived_Throws()
        {
            var entry =
                TestData.CreatePendingEntry();

            entry.Archive(
                TestData.CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    entry.UpdateProfileImage(
                        "profiles/new.webp",
                        TestData.CreatedAtUtc.AddHours(2)));
        }
    }
}
