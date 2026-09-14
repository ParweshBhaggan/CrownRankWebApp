using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task MigrateCrownRankAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Database.MigrateAsync(ct);
    }
}
