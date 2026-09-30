using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Application.Dtos.Category
{
    public class CategoryResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
