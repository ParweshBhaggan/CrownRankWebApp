using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Creators.CreatorService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
