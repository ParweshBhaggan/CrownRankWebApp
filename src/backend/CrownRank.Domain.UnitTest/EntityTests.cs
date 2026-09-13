using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class EntityTests
    {
        [Fact]
        public void Constructor_GeneratesNonEmptyId()
        {
            var entity = new TestEntity();

            Assert.NotEqual(
                Guid.Empty,
                entity.Id);
        }

        [Fact]
        public void Constructor_WithSpecifiedId_UsesId()
        {
            var id = Guid.NewGuid();

            var entity = new TestEntity(id);

            Assert.Equal(
                id,
                entity.Id);
        }

        [Fact]
        public void Constructor_WithEmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new TestEntity(Guid.Empty));
        }

        private sealed class TestEntity : Entity
        {
            public TestEntity()
            {
            }

            public TestEntity(Guid id)
                : base(id)
            {
            }
        }
    }
}
