using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Domain.Models
{
    public abstract class Entity
    {
        public Guid Id
        {
            get;
            protected set;
        }

        public DateTime CreatedDate
        {
            get;
            set;
        }

        public DateTime? UpdatedDate
        {
            get;
            set;
        }

        protected Entity()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.UtcNow;
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
