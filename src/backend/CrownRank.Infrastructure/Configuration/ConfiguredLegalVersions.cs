using CrownRank.Application.Abstractions;

namespace CrownRank.Infrastructure.Configuration;

public sealed class ConfiguredLegalVersions(LegalVersions versions) : ILegalDocumentVersions
{
    public Task<LegalVersions> CurrentAsync(CancellationToken ct = default) => Task.FromResult(versions);
}
