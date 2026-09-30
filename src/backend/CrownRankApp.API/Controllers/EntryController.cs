using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntryController(IEntryServices service) : ControllerBase
    {
        [HttpGet]
        [EndpointSummary("Get all entries")]
        [EndpointDescription("Retrieves a list of all entries.")]
        [ProducesResponseType(typeof(List<EntryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<EntryResponseDto>>> GetEntries()
        {
            var entries = await service.GetAllAsync();
            return Ok(entries.Select(ToResponse).ToList());
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get an entry by ID")]
        [EndpointDescription("Retrieves an entry by its unique identifier.")]
        [ProducesResponseType(typeof(EntryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EntryResponseDto>> GetEntryById(Guid id)
        {
            var entry = await service.GetByIdAsync(id);

            if (entry == null)
            {
                return NotFound($"Entry with id {id} not found.");
            }

            return Ok(ToResponse(entry));
        }

        [HttpPost]
        [EndpointSummary("Add a new entry")]
        [EndpointDescription("Adds a new entry to the system.")]
        [ProducesResponseType(typeof(EntryResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<EntryResponseDto>> AddEntry([FromBody] EntryResponseDto dto)
        {
            try
            {
                var entry = await service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetEntryById), new { id = entry.Id }, ToResponse(entry));
            }
            catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
            catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        }

        // Project the EF graph to a flat contract so category/entry back-references cannot cycle.
        private static EntryResponseDto ToResponse(Entry entry) => new()
        {
            Id = entry.Id, Name = entry.Name, Username = entry.Username, ImgUrl = entry.ImgUrl,
            Score = entry.Score, CreatedDate = entry.CreatedDate, UpdatedDate = entry.UpdatedDate,
            Categories = entry.Categories.Select(category => new CrownRankApp.Application.Dtos.Category.CategoryDto
            {
                Name = category.Name, Description = category.Description
            }).ToList(),
            SocialMediaPlatforms = entry.SocialMediaPlatforms.Select(link => new CrownRankApp.Application.Dtos.SocialMedia.SocialMediaPlatformDto
            {
                PlatformName = link.Platform.Name, Url = link.Url
            }).ToList()
        };

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete an entry by ID")]
        [EndpointDescription("Deletes an existing entry by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteEntry(Guid id)
        {
            var deleted = await service.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound($"Entry with id {id} not found.");
            }

            return NoContent();
        }
    }
}
