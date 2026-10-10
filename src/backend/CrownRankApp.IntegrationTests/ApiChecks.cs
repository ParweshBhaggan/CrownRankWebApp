using CrownRankApp.API.Authentication;
using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Payments;
using CrownRankApp.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

static class ApiChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options)
    {
        const string adminUsername = "integration-admin";
        const string adminPassword = "integration-password";
        const string adminSigningKey = "integration-test-signing-key-32-characters-minimum";

        var gateway = new PaymentChecks.FakeGateway();
        using var factory = new WebApplicationFactory<CrownRankApp.API.Program>().WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "CrownRankApp.API"));
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Stripe:SecretKey"] = "",
                            ["Stripe:WebhookSecret"] = "whsec_test",
                            ["Stripe:LiveMode"] = "false",
                            ["Payments:Currency"] = "usd",
                            ["Payments:MinimumAmount"] = "10",
                            ["Payments:MaximumAmount"] = "10000",
                            ["AdminAuth:Username"] = adminUsername,
                            ["AdminAuth:Password"] = adminPassword,
                            ["AdminAuth:SigningKey"] = adminSigningKey,
                            ["AdminAuth:Issuer"] = "CrownRankApp.Tests",
                            ["AdminAuth:Audience"] = "CrownRankAdmin.Tests",
                            ["AdminAuth:TokenLifetimeMinutes"] = "60"
                        }));
                builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IPaymentGateway>();
                        services.AddSingleton<IPaymentGateway>(gateway);
                        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                        services.AddSingleton(options);
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

        var unauthorizedCategory = await client.PostAsJsonAsync("/api/Category", new CategoryDto
            {
                Name = $"unauthorized_{Guid.NewGuid():N}"
            });
        Check(unauthorizedCategory.StatusCode == HttpStatusCode.Unauthorized, "Admin category mutation rejects missing JWT");

        var rejectedLogin = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(adminUsername, "wrong-password"));
        Check(rejectedLogin.StatusCode == HttpStatusCode.Unauthorized, "Admin login rejects invalid credentials");

        var loginResponse = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(adminUsername, adminPassword));
        var login = await loginResponse.Content.ReadFromJsonAsync<AdminLoginResponse>();
        Check(loginResponse.StatusCode == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(login?.Token), "Admin login issues JWT for valid credentials");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        var securedCategoryName = $"admin_auth_{Guid.NewGuid():N}";
        var securedCategoryResponse = await client.PostAsJsonAsync("/api/Category", new CategoryDto
            {
                Name = securedCategoryName
            });
        var securedCategory = await securedCategoryResponse.Content.ReadFromJsonAsync<CategoryResponseDto>();
        Check(securedCategoryResponse.StatusCode == HttpStatusCode.Created && securedCategory?.Name == securedCategoryName,
            "Admin JWT authorizes protected category mutation");
        var securedCategoryDelete = await client.DeleteAsync($"/api/Category/{securedCategory!.Id}");
        Check(securedCategoryDelete.StatusCode == HttpStatusCode.NoContent, "Admin JWT authorizes protected category deletion");
        client.DefaultRequestHeaders.Authorization = null;

        var settings = await client.GetFromJsonAsync<PaymentSettings>("/api/payments/settings");
        Check(settings is { Currency: "usd", MinimumAmount: 10m, MaximumAmount: 10000m }, "API exposes configurable USD limits");

        using var frontendLogRequest = new HttpRequestMessage(HttpMethod.Get, "/api/payments/settings");
        frontendLogRequest.Headers.Add("X-CrownRank-Client", "frontend");
        Check((await client.SendAsync(frontendLogRequest)).StatusCode == HttpStatusCode.OK, "Frontend-marked API request succeeds");
        var logDate = DateTimeOffset.Now.ToString("dd-MM-yyyy");
        var logRoot = Path.Combine(Directory.GetCurrentDirectory(), "CrownRankApp.API", "Logs");
        var backendLog = Path.Combine(logRoot, "Backend", $"{logDate}.txt");
        var frontendLog = Path.Combine(logRoot, "Frontend", $"{logDate}.txt");
        var backendLogText = File.Exists(backendLog) ? File.ReadAllText(backendLog) : string.Empty;
        var frontendLogText = File.Exists(frontendLog) ? File.ReadAllText(frontendLog) : string.Empty;
        Check(
            backendLogText.Contains("Text: User loaded the payment settings.")
            && backendLogText.Contains("Who: User")
            && backendLogText.Contains("Endpoint: GET /api/payments/settings")
            && backendLogText.Contains("Response: Success")
            && backendLogText.Contains("Request: None"),
            "Backend daily endpoint log is human-readable");
        Check(
            frontendLogText.Contains("Text: User loaded the payment settings.")
            && frontendLogText.Contains("Who: User")
            && frontendLogText.Contains("Endpoint: GET /api/payments/settings")
            && frontendLogText.Contains("Response: Success")
            && frontendLogText.Contains("Request: None"),
            "Frontend daily endpoint log is human-readable and separate");
        var reference = Guid.NewGuid();
        await using var context = new ApplicationDbContext(options);
        var category = await context.Categories.FirstAsync();
        var platform = await context.SocialMediaDefaults.FirstAsync();
        const string image = "data:image/webp;base64,UklGRhIAAABXRUJQVlA4TAYAAAAvAAAAAAfQ//73v/+BiOh/AAA=";
        var originalForm = new EntryResponseDto
        {
            Name = "API recovery creator",
            Username = $"api_{reference:N}",
            ImgUrl = image,
            Score = 99999m,
            Categories = [new CategoryDto
            {
                Name = category.Name
            }],
            SocialMediaPlatforms = [new SocialMediaPlatformDto
            {
                PlatformName = platform.Name,
                Url = "https://example.com/creator"
            }]
        };
        Check((await client.PostAsJsonAsync("/api/Entry", originalForm)).StatusCode == HttpStatusCode.NotFound,
            "Existing entry endpoint cannot publish without a valid payment reference");
        var started = await client.PostAsJsonAsync($"/api/payments/entry-checkout/{reference}", new
            {
                name = originalForm.Name,
                amount = 10.50m
            });
        Check(started.StatusCode == HttpStatusCode.OK, "HTTP checkout accepts only name and amount");
        Check((await client.PostAsJsonAsync($"/api/Entry?paymentId={reference}", originalForm)).StatusCode == HttpStatusCode.Conflict,
            "Existing entry endpoint rejects unpaid form");
        gateway.Pay(reference);
        var confirmed = await (await client.PostAsJsonAsync($"/api/payments/{reference}/confirm", new
            {
            })).Content.ReadFromJsonAsync<PaymentResponse>();
        Check(confirmed is { Status: "paid", Fulfilled: false }, "Payment confirmation waits for the unchanged original form");
        var result = await client.PostAsJsonAsync($"/api/Entry?paymentId={reference}", originalForm);
        var entry = await result.Content.ReadFromJsonAsync<EntryResponseDto>();
        Check(result.StatusCode == HttpStatusCode.Created && entry!.Score == 10.50m && entry.ImgUrl == image,
            "Original entry API registers after payment and preserves the exact image data URL");
        var stored = await context.Entries.SingleAsync(value => value.Id == entry!.Id);
        Check(stored.ImgUrl == image, "HTTP image round trip stores the original bytes without a new upload flow");
        var retries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync($"/api/Entry?paymentId={reference}", originalForm)));
        Check(retries.All(value => value.StatusCode == HttpStatusCode.Created)
                && await context.ScoreAdditions.CountAsync(value => value.EntryId == entry!.Id) == 1 && gateway.Created == 1,
            "Concurrent HTTP return retries register once and never charge again");
        foreach (var retry in retries)
        {
            retry.Dispose();
        }
        var forged = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe-webhook")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        forged.Headers.Add("Stripe-Signature", "invalid");
        Check((await client.SendAsync(forged)).StatusCode == HttpStatusCode.BadRequest, "HTTP webhook rejects forged signatures");
        var boost = Guid.NewGuid();
        await client.PostAsJsonAsync($"/api/payments/entries/{entry!.Id}/boost-checkout/{boost}", new
            {
                name = "Creator boost",
                amount = 10m
            });
        Check((await client.PostAsJsonAsync($"/api/Entry/{entry.Id}/boost?paymentId={boost}", new
                {
                    amount = 99999,
                    referenceId = boost
                })).StatusCode == HttpStatusCode.Conflict,
            "Original boost endpoint cannot credit an unpaid request");
        gateway.Pay(boost);
        using var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe-webhook")
        {
            Content = new StringContent(boost.ToString())
        };
        webhook.Headers.Add("Stripe-Signature", "signed");
        Check((await client.SendAsync(webhook)).StatusCode == HttpStatusCode.OK, "Verified webhook fulfills boost using the existing service");
        var boostResult = await client.PostAsJsonAsync($"/api/Entry/{entry.Id}/boost?paymentId={boost}", new
            {
                amount = 99999,
                referenceId = boost
            });
        var boosted = await boostResult.Content.ReadFromJsonAsync<EntryResponseDto>();
        Check(boostResult.StatusCode == HttpStatusCode.OK && boosted!.Score == 20.50m && boosted.ImgUrl == image,
            "Original boost endpoint ignores forged amounts and returns the unchanged creator image");
    }
}
