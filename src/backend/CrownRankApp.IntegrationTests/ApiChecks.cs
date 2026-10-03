using CrownRankApp.Infrastructure.Payments;
using CrownRankApp.Application.Payments;
using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        var gateway = new PaymentChecks.FakeGateway(TimeProvider.System);
        using var factory = new WebApplicationFactory<CrownRankApp.API.Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "CrownRankApp.API"));
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Stripe:SecretKey"] = "",
                            ["Stripe:WebhookSecret"] = "whsec_example",
                            ["Stripe:LiveMode"] = "false",
                            ["Admin:ApiKey"] = "integration-admin",
                            ["Payments:Currency"] = "usd",
                            ["Payments:MinimumAmount"] = "10",
                            ["Payments:MaximumAmount"] = "10000"
                        }));
                builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IPaymentGateway>();
                        services.AddSingleton<IPaymentGateway>(gateway);
                        services.RemoveAll<StripeSettings>();
                        services.AddSingleton(new StripeSettings
                            {
                                WebhookSecret = "whsec_example"
                            });
                        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                        services.AddSingleton(options);
                        var worker = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                                && descriptor.ImplementationType == typeof(PaymentRecoveryWorker));
                        services.Remove(worker);
                    });
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"FAILED: {message}");
            }
            Console.WriteLine($"PASS: {message}");
        }
        var settings = await client.GetFromJsonAsync<CrownRankApp.Application.Payments.PaymentSettings>("/api/payments/settings");
        Check(settings is
            {
            Currency: "usd", MinimumAmount: 10m, MaximumAmount: 10000m
            }, "API exposes configured USD limits without secrets");
        var entries = await client.GetAsync("/api/Entry");
        Check(entries.StatusCode == HttpStatusCode.OK, "Public leaderboard remains readable");
        var removedCreate = await client.PostAsJsonAsync("/api/Entry", new
            {
                score = 100
            });
        Check(removedCreate.StatusCode == HttpStatusCode.BadRequest, "Entry creation requires a payment reference and valid form");
        var removedBoost = await client.PostAsJsonAsync($"/api/Entry/{Guid.NewGuid()}/boost", new
            {
                amount = 100
            });
        Check(removedBoost.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "Direct public boost mutation is unavailable");
        var invalidAmount = await client.PostAsJsonAsync($"/api/payments/entries/{Guid.NewGuid()}/boost-checkout/{Guid.NewGuid()}", new
            {
                name = "CrownRank creator boost",
                amount = 9.99m
            });
        Check(invalidAmount.StatusCode == HttpStatusCode.BadRequest, "Checkout API enforces the minimum before contacting Stripe");
        var paymentId = Guid.NewGuid();
        await using var context = new ApplicationDbContext(options);
        var category = await context.Categories.FirstAsync();
        var platform = await context.SocialMediaDefaults.FirstAsync(platform => platform.Name == "Instagram");
        using var image = new Image<Rgba32>(1, 1);
        using var imageBytes = new MemoryStream();
        image.SaveAsPng(imageBytes);
        var registration = new EntryRegistrationRequest("API paid creator", $"api_{paymentId:N}",
            "data:image/png;base64," + Convert.ToBase64String(imageBytes.ToArray()), 99999m,
            [new CategoryDto
            {
                Name = category.Name
            }], [new SocialMediaPlatformDto
            {
                PlatformName = platform.Name,
                Url = "https://instagram.com/api_creator"
            }], true);
        var checkoutResponse = await client.PostAsJsonAsync($"/api/payments/entry-checkout/{paymentId}", new
            {
                name = registration.Name,
                amount = 10.50m
            });
        Check(checkoutResponse.StatusCode == HttpStatusCode.OK, "Entry checkout works with only name and amount");
        var started = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();
        Check(started!.Id == paymentId && started.Url != null, "Entry checkout returns its Stripe URL and retry reference");
        var beforePayment = await client.PostAsJsonAsync($"/api/Entry?paymentId={paymentId}", registration);
        Check(beforePayment.StatusCode == HttpStatusCode.Conflict, "Public entry registration verifies payment before publishing");
        gateway.Pay(paymentId);
        var confirmedResponse = await client.PostAsJsonAsync($"/api/payments/{paymentId}/confirm", new
            {
            });
        var confirmed = await confirmedResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Check(confirmed is { Status: "paid", Fulfilled: false }, "API records successful payment while waiting for the form");
        var registered = await client.PostAsJsonAsync($"/api/Entry?paymentId={paymentId}", registration);
        var completed = await registered.Content.ReadFromJsonAsync<PaymentResponse>();
        Check(registered.StatusCode == HttpStatusCode.OK && completed!.Fulfilled, "Entry API registers the form after verified payment without an admin key");
        var retries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync($"/api/Entry?paymentId={paymentId}", registration)));
        Check(retries.All(response => response.StatusCode == HttpStatusCode.OK), "Entry registration safely handles concurrent HTTP retries");
        Check(await context.ScoreAdditions.CountAsync(addition => addition.Id == paymentId) == 1 && gateway.Created == 1,
            "HTTP registration retries create one score addition and no new Stripe session");
        foreach (var response in retries)
        {
            response.Dispose();
        }
        var removedSubmission = await client.PostAsJsonAsync("/api/payments/entry-submissions", registration);
        Check(removedSubmission.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "The extra entry submission endpoint is removed");
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
