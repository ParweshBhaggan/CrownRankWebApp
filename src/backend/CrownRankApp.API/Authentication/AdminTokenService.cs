using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CrownRankApp.API.Authentication;

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AdminLoginResponse(string Token, DateTime ExpiresAtUtc);

public sealed class AdminTokenService(AdminAuthSettings settings, TimeProvider clock)
{
    public AdminLoginResponse? Authenticate(AdminLoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Username, settings.Username, StringComparison.Ordinal)
            || !FixedTimeEquals(request.Password, settings.Password))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.TokenLifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, settings.Username),
                new Claim(ClaimTypes.Name, settings.Username),
                new Claim(ClaimTypes.Role, AdminRoles.Admin),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new AdminLoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static bool FixedTimeEquals(string supplied, string configured)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        var left = SHA256.HashData(suppliedBytes);
        var right = SHA256.HashData(configuredBytes);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
