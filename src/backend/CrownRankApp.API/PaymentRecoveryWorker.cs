using CrownRankApp.Infrastructure.Payments;

namespace CrownRankApp.API;

public sealed class PaymentRecoveryWorker(IServiceScopeFactory scopes, PaymentRecoverySettings settings, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PaymentReconciler>().RunAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError("Payment recovery scan failed with {ErrorType}; the next scan will retry.", exception.GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
