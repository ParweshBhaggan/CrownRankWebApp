using CrownRankApp.Application.Dtos.Entry;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Application.Services.Entry
{
    public interface IEntryServices
    {
        Task<Domain.Models.Entry> CreateAsync(EntryResponseDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<List<Domain.Models.Entry>> GetAllAsync();
        Task<Domain.Models.Entry?> GetByIdAsync(Guid id);
    }
}
