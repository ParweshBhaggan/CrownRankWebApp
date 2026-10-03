using CrownRankApp.Application.Payments;
using CrownRankApp.Application.Dtos.Entry;
using Microsoft.AspNetCore.RateLimiting;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntryController(IEntryServices service, CheckoutService checkout) : ControllerBase
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

        [HttpPost]
        [EnableRateLimiting("payments")]
        [RequestSizeLimit(8 * 1024 * 1024)]
        [EndpointSummary("Register an entry after verified Stripe payment")]
        public async Task<IActionResult> AddEntry([FromQuery] Guid paymentId, EntryRegistrationRequest request, CancellationToken ct)
        {
            if (paymentId == Guid.Empty)
            {
                return Problem(detail: "A payment reference is required.", statusCode: 400);
            }
            try
            {
                var result = await checkout.RegisterEntryAsync(paymentId, request, ct);
                if (result is null)
                {
                    return NotFound();
                }
                return Ok(result);
            }
            catch (ArgumentException exception)
            {
                return Problem(detail: exception.Message, statusCode: 400);
            }
            catch (InvalidOperationException exception)
            {
                return Problem(detail: exception.Message, statusCode: 409);
            }
            catch (PaymentUnavailableException exception)
            {
                return Problem(detail: exception.Message, statusCode: 503);
            }
        }

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete an entry by ID")]
        [EndpointDescription("Deletes an existing entry by its unique identifier.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteEntry(Guid id)
        {
            bool deleted;
            try
            {
                deleted = await service.DeleteAsync(id);
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                    {
                        error = exception.Message
                    });
            }

            if (!deleted)
            {
                return NotFound($"Entry with id {id} not found.");
            }

            return NoContent();
        }
    }
}
