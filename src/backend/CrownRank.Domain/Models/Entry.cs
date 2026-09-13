using CrownRank.Domain.Common;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Models
{
    public sealed class Entry : Entity
    {
        private readonly List<SocialMediaLink> _socialMediaLinks = [];

        public string Name { get; private set; } =
            string.Empty;

        public string Username { get; private set; } =
            string.Empty;

        public Guid CategoryId { get; private set; }

        public string ProfileImageKey { get; private set; } =
            string.Empty;

        public AgreementAcceptance AgreementAcceptance
        {
            get;
            private set;
        } = null!;

        public EntryStatus Status { get; private set; }

        public Guid? InitialContributionId { get; private set; }

        public DateTimeOffset CreatedAtUtc { get; private set; }

        public DateTimeOffset UpdatedAtUtc { get; private set; }

        public DateTimeOffset? PublishedAtUtc { get; private set; }

        public DateTimeOffset? HiddenAtUtc { get; private set; }

        public DateTimeOffset? ArchivedAtUtc { get; private set; }

        public IReadOnlyCollection<SocialMediaLink> SocialMediaLinks =>
            _socialMediaLinks.AsReadOnly();

        private Entry()
        {
        }

        private Entry(
            string name,
            string username,
            Guid categoryId,
            string profileImageKey,
            AgreementAcceptance agreementAcceptance,
            IEnumerable<SocialMediaLink> socialMediaLinks,
            DateTimeOffset createdAtUtc)
        {
            Name = name;
            Username = username;
            CategoryId = categoryId;
            ProfileImageKey = profileImageKey;
            AgreementAcceptance = agreementAcceptance;

            _socialMediaLinks.AddRange(
                socialMediaLinks);

            Status = EntryStatus.PendingPayment;

            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = createdAtUtc;
        }

        public static Entry Create(
            string name,
            string username,
            Category category,
            string profileImageKey,
            AgreementAcceptance agreementAcceptance,
            IEnumerable<SocialMediaLink> socialMediaLinks,
            DateTimeOffset createdAtUtc)
        {
            ValidateName(name);
            ValidateUsername(username);
            ValidateProfileImageKey(
                profileImageKey);

            ArgumentNullException.ThrowIfNull(
                category);

            ArgumentNullException.ThrowIfNull(
                agreementAcceptance);

            if (category.Status !=
                CategoryStatus.Active)
            {
                throw new DomainException(
                    "Entries cannot be created in an archived category.");
            }

            var links =
                ValidateSocialMediaLinks(
                    socialMediaLinks);

            return new Entry(
                name.Trim(),
                username.Trim(),
                category.Id,
                profileImageKey.Trim(),
                agreementAcceptance,
                links,
                createdAtUtc);
        }

        public void Publish(
            RankingContribution initialContribution)
        {
            ArgumentNullException.ThrowIfNull(
                initialContribution);

            if (Status == EntryStatus.Published)
            {
                if (InitialContributionId ==
                    initialContribution.Id)
                {
                    return;
                }

                throw new DomainException(
                    "This entry has already been published using another contribution.");
            }

            if (Status != EntryStatus.PendingPayment)
            {
                throw new DomainException(
                    "Only pending entries can be published.");
            }

            if (initialContribution.EntryId != Id)
            {
                throw new DomainException(
                    "The ranking contribution does not belong to this entry.");
            }

            if (initialContribution.Type !=
                ContributionType.InitialEntryPayment)
            {
                throw new DomainException(
                    "An entry can only be published using its initial payment.");
            }

            if (initialContribution.Status !=
                ContributionStatus.Active)
            {
                throw new DomainException(
                    "An excluded contribution cannot publish an entry.");
            }

            InitialContributionId =
                initialContribution.Id;

            Status = EntryStatus.Published;

            PublishedAtUtc =
                initialContribution.ConfirmedAtUtc;

            UpdatedAtUtc =
                initialContribution.ConfirmedAtUtc;
        }

        public void UpdateBasicDetails(
            string name,
            string username,
            DateTimeOffset updatedAtUtc)
        {
            EnsureEditable();

            ValidateName(name);
            ValidateUsername(username);

            Name = name.Trim();
            Username = username.Trim();
            UpdatedAtUtc = updatedAtUtc;
        }

        public void ChangeCategory(
            Category category,
            DateTimeOffset updatedAtUtc)
        {
            EnsureEditable();

            ArgumentNullException.ThrowIfNull(
                category);

            if (category.Status !=
                CategoryStatus.Active)
            {
                throw new DomainException(
                    "An entry cannot be moved to an archived category.");
            }

            CategoryId = category.Id;
            UpdatedAtUtc = updatedAtUtc;
        }

        public void ReplaceSocialMediaLinks(
            IEnumerable<SocialMediaLink> socialMediaLinks,
            DateTimeOffset updatedAtUtc)
        {
            EnsureEditable();

            var links =
                ValidateSocialMediaLinks(
                    socialMediaLinks);

            _socialMediaLinks.Clear();
            _socialMediaLinks.AddRange(links);

            UpdatedAtUtc = updatedAtUtc;
        }

        public void UpdateProfileImage(
            string profileImageKey,
            DateTimeOffset updatedAtUtc)
        {
            EnsureEditable();

            ValidateProfileImageKey(
                profileImageKey);

            ProfileImageKey =
                profileImageKey.Trim();

            UpdatedAtUtc =
                updatedAtUtc;
        }

        public void Hide(
            DateTimeOffset hiddenAtUtc)
        {
            if (Status == EntryStatus.Hidden)
            {
                return;
            }

            if (Status != EntryStatus.Published)
            {
                throw new DomainException(
                    "Only published entries can be hidden.");
            }

            Status = EntryStatus.Hidden;
            HiddenAtUtc = hiddenAtUtc;
            UpdatedAtUtc = hiddenAtUtc;
        }

        public void Restore(
            DateTimeOffset restoredAtUtc)
        {
            if (Status != EntryStatus.Hidden)
            {
                throw new DomainException(
                    "Only hidden entries can be restored.");
            }

            Status = EntryStatus.Published;
            HiddenAtUtc = null;
            UpdatedAtUtc = restoredAtUtc;
        }

        public void Archive(
            DateTimeOffset archivedAtUtc)
        {
            if (Status == EntryStatus.Archived)
            {
                return;
            }

            Status = EntryStatus.Archived;
            ArchivedAtUtc = archivedAtUtc;
            UpdatedAtUtc = archivedAtUtc;
        }

        private void EnsureEditable()
        {
            if (Status == EntryStatus.Archived)
            {
                throw new DomainException(
                    "Archived entries cannot be modified.");
            }
        }

        private static List<SocialMediaLink>
            ValidateSocialMediaLinks(
                IEnumerable<SocialMediaLink> socialMediaLinks)
        {
            ArgumentNullException.ThrowIfNull(
                socialMediaLinks);

            var links =
                socialMediaLinks.ToList();

            if (links.Count == 0)
            {
                throw new DomainException(
                    "At least one social media account is required.");
            }

            var duplicateUrlExists =
                links
                    .GroupBy(
                        link => link.Url,
                        StringComparer.OrdinalIgnoreCase)
                    .Any(
                        group => group.Count() > 1);

            if (duplicateUrlExists)
            {
                throw new DomainException(
                    "Duplicate social media links are not allowed.");
            }

            return links;
        }

        private static void ValidateName(
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException(
                    "Entry name is required.");
            }

            if (name.Trim().Length > 100)
            {
                throw new DomainException(
                    "Entry name cannot exceed 100 characters.");
            }
        }

        private static void ValidateUsername(
            string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new DomainException(
                    "Username is required.");
            }

            if (username.Trim().Length > 100)
            {
                throw new DomainException(
                    "Username cannot exceed 100 characters.");
            }
        }

        private static void ValidateProfileImageKey(
            string profileImageKey)
        {
            if (string.IsNullOrWhiteSpace(
                    profileImageKey))
            {
                throw new DomainException(
                    "A profile image is required.");
            }

            if (profileImageKey.Trim().Length > 500)
            {
                throw new DomainException(
                    "Profile image reference cannot exceed 500 characters.");
            }
        }
    }
}
