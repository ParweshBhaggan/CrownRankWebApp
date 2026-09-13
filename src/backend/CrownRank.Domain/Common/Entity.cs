using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Common
{
    public abstract class Entity
    {
        public Guid Id { get; protected set; }

        protected Entity()
        {
            Id = Guid.NewGuid();
        }

        protected Entity(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Entity ID cannot be empty.",
                    nameof(id));
            }

            Id = id;
        }
    }
}
