using CrownRankApp.Application.Dtos.Category;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Application.Services.Category
{
    public interface ICategoryService
    {
        Task<List<CategoryResponseDto>> GetAllAsync();

        Task<CategoryResponseDto?> GetByIdAsync(Guid id);

        Task<CategoryResponseDto> CreateAsync(CategoryDto dto);

        Task<CategoryResponseDto> UpdateAsync(Guid id, CategoryDto dto);
        Task<Domain.Models.Category> GetByNameAsync(string name);

        Task<bool> DeleteAsync(Guid id);
    }
}
