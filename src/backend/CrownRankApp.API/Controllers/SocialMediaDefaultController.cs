using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Services.SocialMedia;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SocialMediaDefaultController(ISocialMediaDefaultService service) : ControllerBase
    {
        [HttpGet]
        [EndpointSummary("Get all social media defaults")]
        [EndpointDescription("Retrieves a list of all default social media platforms.")]
        [ProducesResponseType(typeof(List<SocialMediaDefaultResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<SocialMediaDefaultResponseDto>>> GetSocialMediaDefaults()
        {
            var platforms = await service.GetAllAsync();
            return Ok(platforms);
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get a social media default by ID")]
        [EndpointDescription("Retrieves a default social media platform by its unique identifier.")]
        [ProducesResponseType(typeof(SocialMediaDefaultResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SocialMediaDefaultResponseDto>> GetSocialMediaDefaultById(Guid id)
        {
            var platform = await service.GetByIdAsync(id);
            if (platform == null)
            {
                return NotFound($"Social media default with id {id} not found.");
            }

            return Ok(platform);
        }

        [HttpPost]
        [EndpointSummary("Add a new social media default")]
        [EndpointDescription("Adds a new default social media platform to the system.")]
        [ProducesResponseType(typeof(SocialMediaDefaultResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<SocialMediaDefaultResponseDto>> AddSocialMediaDefault([FromBody] SocialMediaDefaultDto dto)
        {
            var platform = await service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetSocialMediaDefaultById), new { id = platform.Id }, platform);
        }

        [HttpPut("{id:guid}")]
        [EndpointSummary("Update a social media default by ID")]
        [EndpointDescription("Updates an existing default social media platform by its unique identifier.")]
        [ProducesResponseType(typeof(SocialMediaDefaultResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SocialMediaDefaultResponseDto>> UpdateSocialMediaDefault(Guid id, [FromBody] SocialMediaDefaultDto dto)
        {
            var existingPlatform = await service.GetByIdAsync(id);
            if (existingPlatform == null)
            {
                return NotFound($"Social media default with id {id} not found.");
            }

            var platform = await service.UpdateAsync(id, dto);
            return Ok(platform);
        }

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete a social media default by ID")]
        [EndpointDescription("Deletes an existing default social media platform by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteSocialMediaDefault(Guid id)
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
