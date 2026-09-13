using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Models
{
    public sealed class Category : Entity
    {
        public string Name { get; private set; } =
            string.Empty;

        public string? Description { get; private set; }

        public CategoryStatus Status { get; private set; }

        public DateTimeOffset CreatedAtUtc { get; private set; }

        public DateTimeOffset UpdatedAtUtc { get; private set; }

        public DateTimeOffset? ArchivedAtUtc { get; private set; }

        private Category()
        {
        }

        private Category(
            string name,
            string? description,
            DateTimeOffset createdAtUtc)
        {
            Name = name;
            Description = description;

            Status = CategoryStatus.Active;

            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = createdAtUtc;
        }

        public static Category Create(
            string name,
            string? description,
            DateTimeOffset createdAtUtc)
        {
            ValidateName(name);
            ValidateDescription(description);

            return new Category(
                name.Trim(),
                description?.Trim(),
                createdAtUtc);
        }

        public void Update(
            string name,
            string? description,
            DateTimeOffset updatedAtUtc)
        {
            EnsureActive();

            ValidateName(name);
            ValidateDescription(description);

            Name = name.Trim();
            Description = description?.Trim();
            UpdatedAtUtc = updatedAtUtc;
        }

        public void Archive(
            DateTimeOffset archivedAtUtc)
        {
            if (Status == CategoryStatus.Archived)
            {
                return;
            }

            Status = CategoryStatus.Archived;
            ArchivedAtUtc = archivedAtUtc;
            UpdatedAtUtc = archivedAtUtc;
        }

        private void EnsureActive()
        {
            if (Status == CategoryStatus.Archived)
            {
                throw new DomainException(
                    "Archived categories cannot be modified.");
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException(
                    "Category name is required.");
            }

            if (name.Trim().Length > 100)
            {
                throw new DomainException(
                    "Category name cannot exceed 100 characters.");
            }
        }

        private static void ValidateDescription(
            string? description)
        {
            if (description is not null &&
                description.Trim().Length > 500)
            {
                throw new DomainException(
                    "Category description cannot exceed 500 characters.");
            }
        }
    }
}
