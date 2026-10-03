using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRankApp.Application.Services.SocialMedia
{
    public interface ISocialMediaPlatformServices
    {
        Task<SocialMediaPlatform> CreateAsync(SocialMediaPlatformDto dto);

        Task<bool> DeleteAsync(Guid id);

        Task<List<SocialMediaPlatform>> GetAllAsync();

        Task<SocialMediaPlatform?> GetByIdAsync(Guid id);
    }
}
