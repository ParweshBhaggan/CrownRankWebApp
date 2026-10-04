using CrownRankApp.API;
using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Data;
using CrownRankApp.Infrastructure.Payments;
using CrownRankApp.Infrastructure.Services.Entry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;

static class HardeningChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options, PaymentChecks.FakeGateway gateway, Guid entryId)
    {
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"FAILED: {message}");
            }
            Console.WriteLine($"PASS: {message}");
        }
        var config = new Dictionary<string, string?>
        {
            ["Admin:ApiKey"] = "random-example-admin-key-with-over-32-characters",
            ["ConnectionStrings:DefaultConnection"] = "test-connection",
            ["Stripe:SecretKey"] = "sk_live_example",
            ["Stripe:WebhookSecret"] = "whsec_example",
            ["Stripe:FrontendUrl"] = "https://crownrank.example",
            ["Stripe:LiveMode"] = "true",
            ["Cors:AllowedOrigins:0"] = "https://crownrank.example"
        };
        IConfiguration Build()
        {
            return new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        }
        void Denied(string message)
        {
            try
            {
                ProductionConfiguration.Validate(Build(), true);
            }
            catch (InvalidOperationException)
            {
                Check(true, message);
                return;
            }
            throw new Exception($"FAILED: {message}");
        }
        ProductionConfiguration.Validate(Build(), true);
        Check(true, "Production accepts external secrets, HTTPS origins and matching live credentials");
        config["Admin:ApiKey"] = "short";
        Denied("Production refuses weak administrator credentials");
        config["Admin:ApiKey"] = "random-example-admin-key-with-over-32-characters";
        config["Stripe:SecretKey"] = "sk_test_example";
        Denied("Production refuses a test key configured for live payments");
        config["Stripe:LiveMode"] = "false";
        ProductionConfiguration.Validate(Build(), true);
        Check(true, "Production hosting can run a safe test-mode staging deployment");
        config["Stripe:FrontendUrl"] = "http://localhost:5173";
        Denied("Production refuses a localhost or non-HTTPS frontend");
        config["Stripe:FrontendUrl"] = "https://crownrank.example";
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config)));
        try
        {
            ProductionConfiguration.Validate(new ConfigurationBuilder().AddJsonStream(json).Build(), true);
            throw new Exception("FAILED: Production accepted JSON secrets");
        }
        catch (InvalidOperationException)
        {
            Check(true, "Production refuses committed JSON secrets");
        }
        ProductionConfiguration.Validate(new ConfigurationBuilder().Build(), false);
        Check(true, "Development startup still works without production secrets");
        var paymentSettings = new PaymentSettings();
        var stripe = new StripeSettings();
        var boost = Guid.NewGuid();
        var staleEntry = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            var payments = new PaymentService(context, new EntryServices(context, TimeProvider.System), gateway, paymentSettings, stripe, TimeProvider.System);
            await payments.StartAsync(boost, new CheckoutRequest("Missed webhook boost", 10m), entryId, default);
            await payments.StartAsync(staleEntry, new CheckoutRequest("Recovered creator", 10m), null, default);
            gateway.Pay(boost);
            gateway.Pay(staleEntry);
            await payments.ConfirmAsync(staleEntry, default);
            await context.CheckoutPayments.Where(payment => payment.Id == staleEntry)
                .ExecuteUpdateAsync(setters => setters.SetProperty(payment => payment.PaidAt, DateTime.UtcNow.AddHours(-1)));
        }
        var createdBefore = gateway.Created;
        var recovery = new PaymentRecoverySettings
        {
            BatchSize = 100
        };
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
                {
                    await using var context = new ApplicationDbContext(options);
                    var payments = new PaymentService(context, new EntryServices(context, TimeProvider.System), gateway, paymentSettings, stripe, TimeProvider.System);
                    await new PaymentReconciler(context, payments, TimeProvider.System, recovery, NullLogger<PaymentReconciler>.Instance).RunAsync(default);
                }));
        await using var lookup = new ApplicationDbContext(options);
        Check(await lookup.ScoreAdditions.CountAsync(addition => addition.Id == boost) == 1, "Concurrent recovery scans credit a missed-webhook boost once");
        Check((await lookup.CheckoutPayments.SingleAsync(payment => payment.Id == boost)).FulfilledEntryId == entryId, "Recovery marks the verified boost fulfilled");
        var pending = await lookup.CheckoutPayments.SingleAsync(payment => payment.Id == staleEntry);
        Check(pending.PaidAt.HasValue && pending.LastCheckedAt.HasValue && pending.FulfilledEntryId is null, "Stale paid entry is flagged without fabricating a profile");
        Check(gateway.Created == createdBefore, "Background recovery never creates another Stripe checkout");
    }
}
