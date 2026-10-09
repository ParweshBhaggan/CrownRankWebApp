using CrownRankApp.API.Authentication;
using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntryController(IEntryServices service, IPaymentService payments) : ControllerBase
    {
        [HttpGet]
        [EndpointSummary("Get all entries")]
        [EndpointDescription("Retrieves a list of all entries.")]
        [ProducesResponseType(typeof(List<EntryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<EntryResponseDto>>> GetEntries()
        {
            var entries = await service.GetAllAsync();
            return Ok(entries.Select(EntryResponseDto.FromEntry).ToList());
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

            return Ok(EntryResponseDto.FromEntry(entry));
        }

        [HttpPost]
        [EndpointSummary("Add a new entry")]
        [EndpointDescription("Adds a new entry to the system after verified payment.")]
        [ProducesResponseType(typeof(EntryResponseDto), StatusCodes.Status201Created)]
        [PaymentErrorFilter]
        [EnableRateLimiting("payments")]
        public async Task<ActionResult<EntryResponseDto>> AddEntry([FromBody] EntryResponseDto dto, [FromQuery] Guid paymentId, CancellationToken ct)
        {
            try
            {
                var entry = await payments.RegisterEntryAsync(paymentId, dto, ct);
                return CreatedAtAction(nameof(GetEntryById), new
                    {
                        id = entry.Id
                    }, entry);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                    {
                        error = exception.Message
                    });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                    {
                        error = exception.Message
                    });
            }
        }

        [HttpPost("{id:guid}/boost")]
        [EndpointSummary("Add a paid positive amount to an entry score")]
        [ProducesResponseType(typeof(EntryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [PaymentErrorFilter]
        [EnableRateLimiting("payments")]
        public async Task<ActionResult<EntryResponseDto>> BoostScore(Guid id, [FromBody] BoostScoreDto dto, [FromQuery] Guid paymentId, CancellationToken ct)
        {
            try
            {
                var entry = await payments.RegisterBoostAsync(paymentId, id, ct);
                return Ok(entry);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new
                    {
                        error = exception.Message
                    });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                    {
                        error = exception.Message
                    });
            }
        }

        [HttpGet("daily")]
        [EndpointSummary("Get a daily ranking by UTC date")]
        [ProducesResponseType(typeof(List<DailyEntryResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DailyEntryResponseDto>>> GetDaily([FromQuery] DateOnly? date)
        {
            var selected = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (selected == DateOnly.MaxValue)
            {
                return BadRequest(new
                    {
                        error = "Date is out of range."
                    });
            }
            return Ok(await service.GetDailyAsync(selected));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AdminRoles.Admin)]
        [EndpointSummary("Delete an entry by ID")]
        [EndpointDescription("Deletes an existing entry by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [PaymentErrorFilter]
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
