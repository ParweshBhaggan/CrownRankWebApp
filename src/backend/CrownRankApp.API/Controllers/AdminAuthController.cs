using CrownRankApp.API.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController(AdminTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("admin-auth")]
    [ProducesResponseType(typeof(AdminLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<AdminLoginResponse> Login([FromBody] AdminLoginRequest request)
    {
        var response = tokens.Authenticate(request);
        return response is null ? Unauthorized() : Ok(response);
    }
}
