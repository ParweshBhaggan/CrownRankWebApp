using CrownRankApp.API.Authentication;
using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Services.SocialMedia;
using CrownRankApp.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SocialMediaPlatformController(ISocialMediaPlatformServices service) : ControllerBase
    {
        [HttpGet]
        [EndpointSummary("Get all social media platforms")]
        [EndpointDescription("Retrieves a list of all social media platforms.")]
        [ProducesResponseType(typeof(List<SocialMediaPlatform>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<SocialMediaPlatform>>> GetSocialMediaPlatforms()
        {
            var platforms = await service.GetAllAsync();
            return Ok(platforms);
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get a social media platform by ID")]
        [EndpointDescription("Retrieves a social media platform by its unique identifier.")]
        [ProducesResponseType(typeof(SocialMediaPlatform), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SocialMediaPlatform>> GetSocialMediaPlatformById(Guid id)
        {
            var platform = await service.GetByIdAsync(id);
            if (platform == null)
            {
                return NotFound($"Social media platform with id {id} not found.");
            }

            return Ok(platform);
        }

        [HttpPost]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Add a new social media platform")]
        [EndpointDescription("Adds a new social media platform to the system.")]
        [ProducesResponseType(typeof(SocialMediaPlatform), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<SocialMediaPlatform>> AddSocialMediaPlatform([FromBody] SocialMediaPlatformDto dto)
        {
            var platform = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetSocialMediaPlatformById), new { id = platform.Id }, platform);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Delete a social media platform by ID")]
        [EndpointDescription("Deletes an existing social media platform by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteSocialMediaPlatform(Guid id)
        {
            var deleted = await service.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
