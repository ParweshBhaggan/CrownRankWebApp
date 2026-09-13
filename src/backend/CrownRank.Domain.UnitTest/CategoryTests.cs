using CrownRank.Domain.Common;
using CrownRank.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class CategoryTests
    {
        private static readonly DateTimeOffset CreatedAtUtc =
            TestData.CreatedAtUtc;

        [Fact]
        public void Create_WithValidData_CreatesActiveCategory()
        {
            var category =
                Category.Create(
                    " Gaming ",
                    " Gaming creators ",
                    CreatedAtUtc);

            Assert.Equal(
                "Gaming",
                category.Name);

            Assert.Equal(
                "Gaming creators",
                category.Description);

            Assert.Equal(
                CategoryStatus.Active,
                category.Status);

            Assert.Equal(
                CreatedAtUtc,
                category.CreatedAtUtc);

            Assert.Equal(
                CreatedAtUtc,
                category.UpdatedAtUtc);

            Assert.Null(
                category.ArchivedAtUtc);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithMissingName_Throws(
            string name)
        {
            Assert.Throws<DomainException>(
                () =>
                    Category.Create(
                        name,
                        null,
                        CreatedAtUtc));
        }

        [Fact]
        public void Create_WithNameLongerThan100Characters_Throws()
        {
            var name =
                new string('A', 101);

            Assert.Throws<DomainException>(
                () =>
                    Category.Create(
                        name,
                        null,
                        CreatedAtUtc));
        }

        [Fact]
        public void Create_WithDescriptionLongerThan500Characters_Throws()
        {
            var description =
                new string('A', 501);

            Assert.Throws<DomainException>(
                () =>
                    Category.Create(
                        "Gaming",
                        description,
                        CreatedAtUtc));
        }

        [Fact]
        public void Update_WhenActive_UpdatesCategory()
        {
            var category =
                Category.Create(
                    "Gaming",
                    "Old",
                    CreatedAtUtc);

            var updatedAt =
                CreatedAtUtc.AddHours(1);

            category.Update(
                " Streamers ",
                " Updated description ",
                updatedAt);

            Assert.Equal(
                "Streamers",
                category.Name);

            Assert.Equal(
                "Updated description",
                category.Description);

            Assert.Equal(
                updatedAt,
                category.UpdatedAtUtc);
        }

        [Fact]
        public void Archive_ArchivesCategory()
        {
            var category =
                TestData.CreateCategory();

            var archivedAt =
                CreatedAtUtc.AddHours(2);

            category.Archive(
                archivedAt);

            Assert.Equal(
                CategoryStatus.Archived,
                category.Status);

            Assert.Equal(
                archivedAt,
                category.ArchivedAtUtc);

            Assert.Equal(
                archivedAt,
                category.UpdatedAtUtc);
        }

        [Fact]
        public void Archive_WhenAlreadyArchived_IsIdempotent()
        {
            var category =
                TestData.CreateCategory();

            var firstTime =
                CreatedAtUtc.AddHours(1);

            var secondTime =
                CreatedAtUtc.AddHours(2);

            category.Archive(
                firstTime);

            category.Archive(
                secondTime);

            Assert.Equal(
                firstTime,
                category.ArchivedAtUtc);

            Assert.Equal(
                firstTime,
                category.UpdatedAtUtc);
        }

        [Fact]
        public void Update_WhenArchived_Throws()
        {
            var category =
                TestData.CreateCategory();

            category.Archive(
                CreatedAtUtc.AddHours(1));

            Assert.Throws<DomainException>(
                () =>
                    category.Update(
                        "New name",
                        null,
                        CreatedAtUtc.AddHours(2)));
        }
    }
}
