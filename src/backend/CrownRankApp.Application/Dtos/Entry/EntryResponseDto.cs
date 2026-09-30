using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;

namespace CrownRankApp.Application.Dtos.Entry
{
    public class EntryResponseDto
    {
        public Guid Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ImgUrl { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public List<CategoryDto> Categories { get; set; } = new();
        public List<SocialMediaPlatformDto> SocialMediaPlatforms { get; set; } = new();
        public static EntryResponseDto FromEntry(Domain.Models.Entry entry) => new()
        {
            Id = entry.Id, Name = entry.Name, Username = entry.Username, ImgUrl = entry.ImgUrl,
            Score = entry.Score, CreatedDate = entry.CreatedDate, UpdatedDate = entry.UpdatedDate,
            Categories = entry.Categories.Select(category => new CategoryDto
            {
                Name = category.Name, Description = category.Description
            }).ToList(),
            SocialMediaPlatforms = entry.SocialMediaPlatforms.Select(link => new SocialMediaPlatformDto
            {
                PlatformName = link.Platform.Name, Url = link.Url
            }).ToList()
        };
    }
}
