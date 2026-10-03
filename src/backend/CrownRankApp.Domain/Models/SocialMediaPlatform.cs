using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Domain.Models
{
    public class SocialMediaPlatform : Entity
    {
        public Guid PlatformId
        {
            get;
            set;
        }

        public SocialMediaDefault Platform
        {
            get;
            set;
        }

        public string Url
        {
            get;
            set;
        } = String.Empty;

        public Guid EntryId
        {
            get;
            set;
        }

        public Entry Entry
        {
            get;
            set;
        }

    }

}
