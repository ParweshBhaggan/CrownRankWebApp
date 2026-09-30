using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;

namespace CrownRankApp.Application.Dtos.Entry
{
    public class EntryResponseDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ImgUrl { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public List<CategoryDto> Categories { get; set; } = new();
        public List<SocialMediaPlatformDto> SocialMediaPlatforms { get; set; } = new();
    }
}
