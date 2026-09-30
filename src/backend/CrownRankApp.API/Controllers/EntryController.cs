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
        [ProducesResponseType(typeof(List<Entry>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<Entry>>> GetEntries()
        {
            var entries = await service.GetAllAsync();
            return Ok(entries);
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get an entry by ID")]
        [EndpointDescription("Retrieves an entry by its unique identifier.")]
        [ProducesResponseType(typeof(Entry), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Entry>> GetEntryById(Guid id)
        {
            var entry = await service.GetByIdAsync(id);

            if (entry == null)
            {
                return NotFound($"Entry with id {id} not found.");
            }

            return Ok(entry);
        }

        [HttpPost]
        [EndpointSummary("Add a new entry")]
        [EndpointDescription("Adds a new entry to the system.")]
        [ProducesResponseType(typeof(Entry), StatusCodes.Status201Created)]
        public async Task<ActionResult<Entry>> AddEntry([FromBody] EntryResponseDto dto)
        {
            var entry = await service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetEntryById),
                new { id = entry.Id },
                entry
            );
        }

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
