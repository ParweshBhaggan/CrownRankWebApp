using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrownRank.ApiTests;

internal sealed class TestApiFactory(string environment = "Development") : WebApplicationFactory<Program>
{
    internal ApiStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Database", "Host=localhost;Database=unused_tests;Username=test;Password=test;Timeout=1");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICreatorRepository>();
            services.RemoveAll<IPaymentGateway>();
            services.RemoveAll<IProfileImageService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(Store);
            services.AddSingleton<ICreatorRepository>(Store);
            services.AddSingleton<IPaymentGateway>(Store);
            services.AddSingleton<IProfileImageService>(Store);
            services.AddSingleton<TimeProvider>(Store.Clock);
        });
    }

    internal HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });
}

internal sealed class ApiStore : ICreatorRepository, IPaymentGateway, IProfileImageService
{
    internal List<Creator> Creators { get; } = [];
    internal FixedApiClock Clock { get; } = new();
    internal List<CheckoutRequest> CheckoutRequests { get; } = [];

    public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Creator>>(Creators);
    public Task<Creator?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Creators.SingleOrDefault(x => x.Id == id));
    public Task<Creator?> GetByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult(Creators.SingleOrDefault(x => x.Username == username));
    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) => Task.FromResult(Creators.Any(x => x.Username == username));
    public Task<Creator?> GetByEntryReferenceAsync(Guid reference, CancellationToken cancellationToken) => Task.FromResult(Creators.SingleOrDefault(x => x.EntryReference == reference));
    public Task<Contribution?> GetContributionAsync(string reference, CancellationToken cancellationToken) => Task.FromResult(Creators.SelectMany(x => x.Contributions).SingleOrDefault(x => x.PaymentReference == reference));
    public Task AddAsync(Creator creator, CancellationToken cancellationToken) { Creators.Add(creator); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        CheckoutRequests.Add(request);
        return Task.FromResult(new CheckoutSession($"mock-{request.ReferenceId:N}", true));
    }

    public Task<StoredProfileImage> SaveAsync(ProfileImageUpload upload, CancellationToken cancellationToken) =>
        Task.FromResult(new StoredProfileImage("/uploads/test.webp", "test.webp"));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixedApiClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
}
