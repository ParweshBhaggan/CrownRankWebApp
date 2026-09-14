using CrownRank.Application.Abstractions;

namespace CrownRank.ConsoleApp;

// Local test UI only. No API or production host should register this implementation.
public sealed class ConsoleAdminAuthorization : IAdminAuthorization
{
    public Task<bool> IsAuthorizedAsync(string credential, CancellationToken ct = default)
        => Task.FromResult(credential == "console-admin");
}
