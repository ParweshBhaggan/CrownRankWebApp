using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrownRank.Infrastructure.Persistence;

// Used by dotnet ef. Runtime hosts configure their provider and connection separately.
public sealed class CrownRankDesignTimeFactory : IDesignTimeDbContextFactory<CrownRankDbContext>
{
    public CrownRankDbContext CreateDbContext(string[] args)
    {
        var provider =
            Environment.GetEnvironmentVariable(
                "CrownRank__DatabaseProvider")
            ?? "Sqlite";

        var options =
            new DbContextOptionsBuilder<CrownRankDbContext>();

        if (provider.Equals(
                "PostgreSQL",
                StringComparison.OrdinalIgnoreCase))
        {
            var connectionString =
                Environment.GetEnvironmentVariable(
                    "ConnectionStrings__CrownRank");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings__CrownRank is required for PostgreSQL.");
            }

            options.UseNpgsql(connectionString);
        }
        else
        {
            var connectionString =
                Environment.GetEnvironmentVariable(
                    "CROWNRANK_DESIGN_CONNECTION")
                ?? "Data Source=crownrank-design.db";

            options.UseSqlite(connectionString);
        }

        return new CrownRankDbContext(options.Options);
    }
}
