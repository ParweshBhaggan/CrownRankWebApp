using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Domain.Models
{
    public class Category : Entity
    {
        public string Name { get; set; } = String.Empty;
        public string Description { get; set; } = String.Empty;

        public List<Entry> Entries { get; set; } = new List<Entry>();

    }
}
