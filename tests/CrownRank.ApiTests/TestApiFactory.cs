using CrownRank.Api.Data;
using CrownRank.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrownRank.ApiTests;

internal sealed class TestApiFactory(string environment = "Development") : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<CrownRankDbContext>();
            services.RemoveAll<DbContextOptions<CrownRankDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CrownRankDbContext>>();
            services.RemoveAll<IProfileImageStore>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(_connection);
            services.AddDbContext<CrownRankDbContext>((provider, options) =>
                options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
            services.AddSingleton<IProfileImageStore, FakeImageStore>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        });
    }

    internal HttpClient CreateHttpsClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Database.EnsureCreated();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

internal sealed class FakeImageStore : IProfileImageStore
{
    public Task<StoredImage> SaveAsync(IFormFile image, CancellationToken cancellationToken) =>
        Task.FromResult(new StoredImage("/uploads/profiles/test.png", "test.png"));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
}
