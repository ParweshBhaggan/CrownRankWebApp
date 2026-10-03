using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Services.Category;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Services.Category
{
    public class CategoryServices(ApplicationDbContext context) : ICategoryService
    {
        public async Task<List<CategoryResponseDto>> GetAllAsync()
        {
            return await context.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description
                }).ToListAsync();
        }

        public Task<CategoryResponseDto?> GetByIdAsync(Guid id)
        {
            return context.Categories.AsNoTracking().Where(c => c.Id == id).Select(c => new CategoryResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description
                }).FirstOrDefaultAsync();
        }

        public async Task<Domain.Models.Category> GetByNameAsync(string name)
        {

            return await context.Categories.AsNoTracking().Where(c => c.Name == name).FirstOrDefaultAsync();

        }

        public async Task<CategoryResponseDto> CreateAsync(CategoryDto dto)
        {
            var exists = await context.Categories
                .AnyAsync(x => x.Name == dto.Name);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Category '{dto.Name}' already exists.");
            }

            var category = new Domain.Models.Category
            {

                Name = dto.Name,
                Description = dto.Description.Trim()
            };

            context.Categories.Add(category);

            await context.SaveChangesAsync();

            return new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            };
        }

        public async Task<CategoryResponseDto> UpdateAsync(Guid id, CategoryDto dto)
        {
            var category = await context.Categories.FirstOrDefaultAsync(x => x.Id == id);

            if (category == null)
            {
                return new CategoryResponseDto();
            }

            var name = dto.Name.Trim();

            var nameAlreadyExists = await context.Categories
                .AnyAsync(x =>
                x.Name == name &&
                x.Id != id);

            if (nameAlreadyExists)
            {
                throw new InvalidOperationException(
                    $"Category '{name}' already exists.");
            }

            category.Name = name;
            category.Description = dto.Description.Trim();
            category.UpdatedDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            };
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var locked = await context.Categories.Where(row => row.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Name, row => row.Name));
            if (locked == 0)
            {
                return false;
            }
            if (await context.PaymentOperations.AnyAsync(payment => payment.Purpose == CrownRankApp.Domain.Models.PaymentPurpose.Entry
                    && payment.FulfilledAt == null && payment.Status != CrownRankApp.Domain.Models.PaymentStatus.Expired
                    && payment.Status != CrownRankApp.Domain.Models.PaymentStatus.Failed))
            {
                throw new InvalidOperationException("Lookup deletion is unavailable while an entry payment is active.");
            }

            var category = await context.Categories
                .FirstOrDefaultAsync(x => x.Id == id);

            if (category == null)
            {
                return false;
            }

            context.Categories.Remove(category);

            await context.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }

    }
}
