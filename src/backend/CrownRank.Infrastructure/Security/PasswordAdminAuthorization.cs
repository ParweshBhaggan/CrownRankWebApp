using System.Security.Cryptography;
using CrownRank.Application.Abstractions;

namespace CrownRank.Infrastructure.Security;

// This is a bootstrap credential check, not a session or user-management system.
// Supply a randomly generated salt and PBKDF2-SHA256 hash through secret configuration.
public sealed class PasswordAdminAuthorization(string saltBase64, string hashBase64) : IAdminAuthorization
{
    private readonly byte[] _salt = Convert.FromBase64String(saltBase64);
    private readonly byte[] _hash = Convert.FromBase64String(hashBase64);

    public Task<bool> IsAuthorizedAsync(string credential, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_salt.Length < 16 || _hash.Length < 32) throw new InvalidOperationException("Admin credential configuration is invalid.");
        var candidate = Rfc2898DeriveBytes.Pbkdf2(credential ?? string.Empty, _salt, 210_000, HashAlgorithmName.SHA256, _hash.Length);
        return Task.FromResult(CryptographicOperations.FixedTimeEquals(candidate, _hash));
    }
}
