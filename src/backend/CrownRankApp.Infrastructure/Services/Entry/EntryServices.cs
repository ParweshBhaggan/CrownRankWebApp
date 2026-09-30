using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Services.Entry
{
    public class EntryServices(ApplicationDbContext context) : IEntryServices
    {
        private IQueryable<Domain.Models.Entry> EntriesWithProfiles() => context.Entries
            .AsNoTracking().AsSplitQuery()
            .Include(entry => entry.Categories)
            .Include(entry => entry.SocialMediaPlatforms).ThenInclude(link => link.Platform);

        public Task<List<Domain.Models.Entry>> GetAllAsync() => EntriesWithProfiles()
            .OrderByDescending(entry => entry.Score).ThenBy(entry => entry.CreatedDate).ThenBy(entry => entry.Id)
            .ToListAsync();

        public Task<Domain.Models.Entry?> GetByIdAsync(Guid id) => EntriesWithProfiles()
            .FirstOrDefaultAsync(entry => entry.Id == id);

        public async Task<Domain.Models.Entry> CreateAsync(EntryResponseDto dto)
        {
            var name = dto.Name.Trim();
            var username = dto.Username.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Name and username are required.");
            if (dto.Score < 1 || dto.Score > 10000 || decimal.Round(dto.Score, 2) != dto.Score)
                throw new ArgumentException("Score must be between 1 and 10000 with at most two decimal places.");
            if (string.IsNullOrWhiteSpace(dto.ImgUrl))
                throw new ArgumentException("A profile image URL is required.");
            if (dto.Categories == null || dto.Categories.Count == 0)
                throw new ArgumentException("At least one category is required.");
            if (dto.SocialMediaPlatforms == null || dto.SocialMediaPlatforms.Count is < 1 or > 5)
                throw new ArgumentException("Add between one and five social profiles.");
            if (await context.Entries.AnyAsync(entry => entry.Username == username))
                throw new InvalidOperationException("An entry with this username already exists.");

            var categoryNames = dto.Categories.Select(category => category.Name.Trim()).Distinct().ToList();
            // Track existing lookup entities so EF reuses them instead of inserting them again.
            var categories = await context.Categories.Where(category => categoryNames.Contains(category.Name)).ToListAsync();
            if (categories.Count != categoryNames.Count)
                throw new ArgumentException("One or more selected categories are unavailable.");
            var platformNames = dto.SocialMediaPlatforms.Select(link => link.PlatformName.Trim()).ToList();
            if (platformNames.Distinct().Count() != platformNames.Count)
                throw new ArgumentException("Social platforms must be unique.");
            var platforms = await context.SocialMediaDefaults.Where(platform => platformNames.Contains(platform.Name)).ToListAsync();
            if (platforms.Count != platformNames.Count)
                throw new ArgumentException("One or more selected social platforms are unavailable.");

            var entry = new Domain.Models.Entry
            {
                Name = name, Username = username, ImgUrl = dto.ImgUrl, Score = dto.Score, Categories = categories
            };
            foreach (var link in dto.SocialMediaPlatforms)
            {
                if (!Uri.TryCreate(link.Url.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.UserInfo))
                    throw new ArgumentException("Social profiles require valid HTTPS URLs.");
                var platform = platforms.Single(platform => platform.Name == link.PlatformName.Trim());
                entry.SocialMediaPlatforms.Add(new SocialMediaPlatform
                {
                    PlatformId = platform.Id, Platform = platform, Url = link.Url.Trim(), Entry = entry
                });
            }
            // Save the entry and all links together; the entry FK exists before links are inserted.
            context.Entries.Add(entry);
            await context.SaveChangesAsync();
            return entry;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entry = await context.Entries.FirstOrDefaultAsync(entry => entry.Id == id);
            if (entry == null) return false;
            context.Entries.Remove(entry); // Social links are deleted by the configured cascade.
            await context.SaveChangesAsync();
            return true;
        }
    }
}
