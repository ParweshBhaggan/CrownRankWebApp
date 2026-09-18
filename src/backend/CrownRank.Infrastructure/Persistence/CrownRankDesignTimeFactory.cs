using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrownRank.Infrastructure.Persistence;

// Used by dotnet ef. Runtime hosts configure their provider and connection separately.
public sealed class CrownRankDesignTimeFactory : IDesignTimeDbContextFactory<CrownRankDbContext>
{
    public CrownRankDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("CrownRank__DatabaseProvider")
            ?? "PostgreSQL";
        var options = new DbContextOptionsBuilder<CrownRankDbContext>();

        if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CrownRank")
                ?? "Host=localhost;Database=crownrank_design;Username=postgres";
            options.UseNpgsql(connectionString);
        }
        else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = Environment.GetEnvironmentVariable("CROWNRANK_DESIGN_CONNECTION")
                ?? "Data Source=crownrank-design.db";
            options.UseSqlite(connectionString);
        }
        else
        {
            throw new InvalidOperationException(
                "CrownRank__DatabaseProvider must be PostgreSQL or Sqlite.");
        }

        return new CrownRankDbContext(options.Options);
    }
}
