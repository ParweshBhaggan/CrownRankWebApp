using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Creators.CreatorService>();
        services.AddScoped<Creators.PaymentCheckoutService>();
        services.AddScoped<Creators.PaymentConfirmationService>();
        services.AddScoped<Creators.PaymentStatusService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
