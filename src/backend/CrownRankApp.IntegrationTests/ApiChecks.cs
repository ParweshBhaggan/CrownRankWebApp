using System.Net;
using System.Net.Http.Json;
using CrownRankApp.API;
using CrownRankApp.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

static class ApiChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options)
    {
        using var factory = new WebApplicationFactory<CrownRankApp.API.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "CrownRankApp.API"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "", ["Stripe:WebhookSecret"] = "whsec_example", ["Stripe:LiveMode"] = "false",
                ["Admin:ApiKey"] = "integration-admin", ["Payments:Currency"] = "usd",
                ["Payments:MinimumAmount"] = "10", ["Payments:MaximumAmount"] = "10000"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddSingleton(options);
                var worker = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(PaymentRecoveryWorker));
                services.Remove(worker);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception($"FAILED: {message}");
            Console.WriteLine($"PASS: {message}");
        }
        var settings = await client.GetFromJsonAsync<CrownRankApp.Application.Payments.PaymentSettings>("/api/payments/settings");
        Check(settings is { Currency: "usd", MinimumAmount: 10m, MaximumAmount: 10000m }, "API exposes configured USD limits without secrets");
        var entries = await client.GetAsync("/api/Entry");
        Check(entries.StatusCode == HttpStatusCode.OK, "Public leaderboard remains readable");
        var removedCreate = await client.PostAsJsonAsync("/api/Entry", new { score = 100 });
        Check(removedCreate.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "Direct public entry creation is unavailable");
        var removedBoost = await client.PostAsJsonAsync($"/api/Entry/{Guid.NewGuid()}/boost", new { amount = 100 });
        Check(removedBoost.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "Direct public boost mutation is unavailable");
        var invalidAmount = await client.PostAsJsonAsync("/api/payments/boost-checkout", new
        {
            referenceId = Guid.NewGuid(), entryId = Guid.NewGuid(), amount = 9.99m
        });
        Check(invalidAmount.StatusCode == HttpStatusCode.BadRequest, "Checkout API enforces the minimum before contacting Stripe");
        var unauthorized = await client.DeleteAsync($"/api/Entry/{Guid.NewGuid()}");
        Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Administrative mutations require credentials");
        client.DefaultRequestHeaders.Add("X-Admin-Key", "integration-admin");
        var authorized = await client.DeleteAsync($"/api/Entry/{Guid.NewGuid()}");
        Check(authorized.StatusCode == HttpStatusCode.NotFound, "Configured administrator credential permits mutation routing");
        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        using var forged = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe-webhook")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        forged.Headers.Add("Stripe-Signature", "t=1,v1=invalid");
        Check((await client.SendAsync(forged)).StatusCode == HttpStatusCode.BadRequest, "Webhook API rejects a forged signature");
    }
}
