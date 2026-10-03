using CrownRankApp.Application.Payments;

namespace CrownRankApp.API;

public sealed class PaymentRecoveryWorker(IServiceScopeFactory scopes, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var listing = scopes.CreateScope();
                var operations = await listing.ServiceProvider.GetRequiredService<IPaymentStore>().GetPendingAsync(stoppingToken);
                foreach (var operation in operations)
                {
                    try
                    {
                        using var processing = scopes.CreateScope();
                        await processing.ServiceProvider.GetRequiredService<CheckoutService>().ResumeAsync(operation.Id, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(exception, "Payment {PaymentId} requires recovery", operation.Id);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Payment recovery scan failed");
            }
        }
    }
}
