using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrownRank.Infrastructure.Persistence;

// Used by dotnet ef. Runtime hosts configure their provider and connection separately.
public sealed class CrownRankDesignTimeFactory : IDesignTimeDbContextFactory<CrownRankDbContext>
{
    public CrownRankDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CROWNRANK_DESIGN_CONNECTION") ?? "Data Source=crownrank-design.db";
        return new CrownRankDbContext(new DbContextOptionsBuilder<CrownRankDbContext>().UseSqlite(connection).Options);
    }
}
