using CrownRankApp.Application.Dtos.SocialMedia;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Application.Services.SocialMedia
{
    public interface ISocialMediaDefaultService
    {
        Task<List<SocialMediaDefaultResponseDto>> GetAllAsync();

        Task<SocialMediaDefaultResponseDto?> GetByIdAsync(Guid id);

        Task<SocialMediaDefaultResponseDto> CreateAsync(SocialMediaDefaultDto dto);

        Task<SocialMediaDefaultResponseDto> UpdateAsync(Guid id, SocialMediaDefaultDto dto);

        Task<bool> DeleteAsync(Guid id);
    }
}
