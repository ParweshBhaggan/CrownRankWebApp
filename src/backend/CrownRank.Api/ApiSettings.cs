using CrownRank.Application.Abstractions;

namespace CrownRank.Api;

public sealed class ApiSettings
{
    public string DatabasePath { get; init; } = "data/crownrank.db";
    public string ImageDirectory { get; init; } = "data/assets";
    public bool EnableMockPayments { get; init; }
    public string AdminPassword { get; init; } = string.Empty;
    public string TermsVersion { get; init; } = "terms-v1";
    public string PrivacyVersion { get; init; } = "privacy-v1";
    public string RulesVersion { get; init; } = "rules-v1";
    public string[] AllowedOrigins { get; init; } = [];
}

public sealed class ConfiguredAdminAuthorization(string configuredPassword) : IAdminAuthorization
{
    public Task<bool> IsAuthorizedAsync(string credential, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(configuredPassword))
            throw new InvalidOperationException("CrownRank:AdminPassword must be configured.");
        var supplied = System.Text.Encoding.UTF8.GetBytes(credential ?? string.Empty);
        var expected = System.Text.Encoding.UTF8.GetBytes(configuredPassword);
        return Task.FromResult(supplied.Length == expected.Length &&
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(supplied, expected));
    }
}
