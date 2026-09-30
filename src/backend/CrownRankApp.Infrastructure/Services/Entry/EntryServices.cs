using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Services.Category;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Application.Services.SocialMedia;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Infrastructure.Services.Entry
{
    public class EntryServices(ApplicationDbContext context, ICategoryService catService, ISocialMediaPlatformServices socialService) : IEntryServices
    {

        public async Task<List<Domain.Models.Entry>> GetAllAsync()
        {
            return await context.Entries
            .AsNoTracking()
            .Include(x => x.Categories)
            .Include(x => x.SocialMediaPlatforms)
            .OrderBy(x => x.Name)
            .ToListAsync();
        }

        public async Task<Domain.Models.Entry?> GetByIdAsync(Guid id)
        {
            return await context.Entries
            .AsNoTracking()
            .Include(x => x.Categories)
            .Include(x => x.SocialMediaPlatforms)
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync();
        }

        public async Task<Domain.Models.Entry> CreateAsync(EntryResponseDto dto)
        {
            var entry = new Domain.Models.Entry
            {
                Name = dto.Name,
                Username = dto.Username,
                ImgUrl = dto.ImgUrl,
                Score = dto.Score,


            };
            var categories = dto.Categories;
            var socialMediaPlatforms = dto.SocialMediaPlatforms;
            foreach (var category in categories)
            {

                var _category = await catService.GetByNameAsync(category.Name);

                entry.Categories.Add(_category);
            }

            foreach (var socialMediaPlatform in socialMediaPlatforms)
            {
                var _socialMediaPlatform = await socialService.CreateAsync(socialMediaPlatform);
                entry.SocialMediaPlatforms.Add(_socialMediaPlatform);
            }

            context.Entries.Add(entry);
            await context.SaveChangesAsync();
            return entry;

        }

        //delete
        public async Task<bool> DeleteAsync(Guid id)
        {
            var entry = await GetByIdAsync(id);
            if (entry == null)
                return false;

            //delete social media platforms
            foreach (var socialMediaPlatform in entry.SocialMediaPlatforms)
            {
                await socialService.DeleteAsync(socialMediaPlatform.Id);
            }

            context.Entries.Remove(entry);
            await context.SaveChangesAsync();
            return true;
        }

    }
}
