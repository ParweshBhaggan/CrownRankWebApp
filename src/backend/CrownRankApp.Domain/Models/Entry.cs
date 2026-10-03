using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Domain.Models
{
    public class Entry : Entity
    {
        public Entry() { }
        public Entry(Guid id) : base(id) { }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ImgUrl { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<SocialMediaPlatform> SocialMediaPlatforms { get; set; } = new List<SocialMediaPlatform>();

    }
}

