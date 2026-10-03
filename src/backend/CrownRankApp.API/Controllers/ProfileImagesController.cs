using CrownRankApp.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/profile-images")]
[EnableRateLimiting("payments")]
public sealed class ProfileImagesController(ProfileImageStorage images) : ControllerBase
{
    [HttpPost("{referenceId:guid}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid referenceId, IFormFile file, CancellationToken ct)
    {
        if (referenceId == Guid.Empty || file.Length == 0 || file.Length > 5 * 1024 * 1024)
        {
            return Problem(detail: "Choose a JPG, PNG, or WebP image up to 5 MB.", statusCode: 400);
        }
        try
        {
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes, ct);
            var url = await images.UploadAsync(referenceId, bytes.ToArray(), ct);
            return Ok(new
                {
                    url
                });
        }
        catch (ArgumentException exception)
        {
            return Problem(detail: exception.Message, statusCode: 400);
        }
    }
}
