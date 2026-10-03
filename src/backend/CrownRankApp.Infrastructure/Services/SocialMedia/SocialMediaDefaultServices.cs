using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Services.SocialMedia;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Services.SocialMedia
{
    public class SocialMediaDefaultServices(ApplicationDbContext context) : ISocialMediaDefaultService
    {
        public async Task<List<SocialMediaDefaultResponseDto>> GetAllAsync()
        {
            return await context.SocialMediaDefaults
           .AsNoTracking()
           .OrderBy(x => x.Name)
           .Select(x => new SocialMediaDefaultResponseDto
           {
               Id = x.Id,
               Name = x.Name
           })
           .ToListAsync();
        }

        public async Task<SocialMediaDefaultResponseDto?> GetByIdAsync(Guid id)
        {
            return await context.SocialMediaDefaults
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SocialMediaDefaultResponseDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .FirstOrDefaultAsync();
        }
        public async Task<SocialMediaDefaultResponseDto> CreateAsync(SocialMediaDefaultDto dto)
        {
            var name = dto.Name.Trim();
            var exists = await context.SocialMediaDefaults
            .AnyAsync(x => x.Name == dto.Name);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Social media platform '{dto.Name}' already exists.");
            }

            var platform = new SocialMediaDefault
            {

                Name = dto.Name     
            };

            context.SocialMediaDefaults.Add(platform);

            await context.SaveChangesAsync();

            return new SocialMediaDefaultResponseDto
            {
                Id = platform.Id,
                Name = platform.Name
            };
        }

        public async Task<SocialMediaDefaultResponseDto> UpdateAsync(Guid id, SocialMediaDefaultDto dto)
        {
            var platform = await context.SocialMediaDefaults
             .FirstOrDefaultAsync(x => x.Id == id);

            if (platform == null)
            {
                return new SocialMediaDefaultResponseDto
                {
                    Id = id,
                    Name = dto.Name
                };
            }

            var name = dto.Name.Trim();

            var exists = await context.SocialMediaDefaults
                .AnyAsync(x =>
                    x.Name == dto.Name &&
                    x.Id != id);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Social media platform '{dto.Name}' already exists.");
            }

            platform.Name = dto.Name;
            platform.UpdatedDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return new SocialMediaDefaultResponseDto
            {
                Id = platform.Id,
                Name = platform.Name
            };
        }
        public async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var locked = await context.SocialMediaDefaults.Where(row => row.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Name, row => row.Name));
            if (locked == 0) return false;
            if (await context.PaymentOperations.AnyAsync(payment => payment.Purpose == CrownRankApp.Domain.Models.PaymentPurpose.Entry
                && payment.FulfilledAt == null && payment.Status != CrownRankApp.Domain.Models.PaymentStatus.Expired
                && payment.Status != CrownRankApp.Domain.Models.PaymentStatus.Failed))
                throw new InvalidOperationException("Lookup deletion is unavailable while an entry payment is active.");

            var platform = await context.SocialMediaDefaults
            .FirstOrDefaultAsync(x => x.Id == id);

            if (platform == null)
            {
                return false;
            }

            context.SocialMediaDefaults.Remove(platform);

            await context.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }
    }
}

