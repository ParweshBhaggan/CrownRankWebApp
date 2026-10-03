using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Services.SocialMedia;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Services.SocialMedia
{
    public class SocialMediaPlatformServices(ApplicationDbContext context) : ISocialMediaPlatformServices
    {
        public async Task<List<SocialMediaPlatform>> GetAllAsync()
        {
            return await context.SocialMediaPlatforms
                .AsNoTracking()
                .Include(p => p.Platform)
                .OrderBy(x => x.Platform.Name)
                .ToListAsync();
        }

        public async Task<SocialMediaPlatform?> GetByIdAsync(Guid id)
        {
            return await context.SocialMediaPlatforms
                .AsNoTracking()
                .Include(p => p.Platform)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<SocialMediaPlatform> CreateAsync(SocialMediaPlatformDto dto)
        {
            dto.PlatformName = dto.PlatformName.Trim();
            var PlatformDefault = await context.SocialMediaDefaults.AsNoTracking().Where(p => p.Name == dto.PlatformName).FirstOrDefaultAsync();

            var Platform = new SocialMediaPlatform
            {
                PlatformId = PlatformDefault.Id,
                Platform = PlatformDefault,
                Url = dto.Url
            };

            await context.SocialMediaPlatforms.AddAsync(Platform);
            await context.SaveChangesAsync();
            return Platform;

        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var Platform = await GetByIdAsync(id);
            if (Platform == null)
            {
                return false;
            }

            context.SocialMediaPlatforms.Remove(Platform);
            await context.SaveChangesAsync();
            return true;
        }
    }
}
