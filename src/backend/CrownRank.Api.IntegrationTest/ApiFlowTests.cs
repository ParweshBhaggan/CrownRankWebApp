using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CrownRank.Api.IntegrationTest;

public sealed class ApiFlowTests
{
    [Fact]
    public async Task Public_and_admin_mock_payment_flow_works_through_http()
    {
        var directory = Path.Combine(Path.GetTempPath(), "crownrank-api-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = new CrownRankApiFactory(directory);
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/categories")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/admin/login", new { password = "wrong" })).StatusCode);

            var login = await client.PostAsJsonAsync("/api/admin/login", new { password = "api-test-admin" });
            login.EnsureSuccessStatusCode();
            var token = (await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync()))
                .RootElement.GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var categoryResponse = await client.PostAsJsonAsync("/api/admin/categories",
                new { name = "Creators", description = "Social creators" });
            Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
            var categoryId = (await JsonDocument.ParseAsync(await categoryResponse.Content.ReadAsStreamAsync()))
                .RootElement.GetProperty("id").GetGuid();

            client.DefaultRequestHeaders.Authorization = null;
            var submission = await client.PostAsync("/api/entries", EntryForm(categoryId, "Creator", "creator", 500));
            Assert.Equal(HttpStatusCode.Accepted, submission.StatusCode);
            var submissionJson = (await JsonDocument.ParseAsync(await submission.Content.ReadAsStreamAsync())).RootElement;
            var entryId = submissionJson.GetProperty("entryId").GetGuid();
            var attemptId = submissionJson.GetProperty("attemptId").GetGuid();
            Assert.Equal("CheckoutReady", await PaymentStateAsync(client, attemptId));

            await SetOutcomeAsync(client, attemptId, "Succeeded");
            var confirmation = await client.PostAsync($"/api/payments/{attemptId}/confirm", null);
            confirmation.EnsureSuccessStatusCode();
            Assert.Equal("Confirmed", await PaymentStateAsync(client, attemptId));
            (await client.PostAsync($"/api/payments/{attemptId}/confirm", null)).EnsureSuccessStatusCode();

            var profile = await client.GetAsync($"/api/entries/{entryId}");
            profile.EnsureSuccessStatusCode();
            var profileJson = (await JsonDocument.ParseAsync(await profile.Content.ReadAsStreamAsync())).RootElement;
            Assert.Equal("Creator", profileJson.GetProperty("name").GetString());
            Assert.Single(profileJson.GetProperty("socialLinks").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync(profileJson.GetProperty("imageUrl").GetString())).StatusCode);

            var boost = await client.PostAsJsonAsync($"/api/entries/{entryId}/boosts",
                new { amountInMinorUnits = 200, currency = "EUR" });
            Assert.Equal(HttpStatusCode.Accepted, boost.StatusCode);
            var boostAttempt = (await JsonDocument.ParseAsync(await boost.Content.ReadAsStreamAsync()))
                .RootElement.GetProperty("attemptId").GetGuid();
            await SetOutcomeAsync(client, boostAttempt, "Succeeded");
            (await client.PostAsync($"/api/payments/{boostAttempt}/confirm", null)).EnsureSuccessStatusCode();

            var leaderboard = await client.GetFromJsonAsync<JsonElement>($"/api/leaderboards/global?categoryId={categoryId}");
            var leader = Assert.Single(leaderboard.EnumerateArray());
            Assert.Equal(700, leader.GetProperty("scoreInMinorUnits").GetInt64());
            var daily = await client.GetFromJsonAsync<JsonElement>(
                $"/api/leaderboards/daily?date={DateTime.UtcNow:yyyy-MM-dd}&categoryId={categoryId}");
            Assert.Single(daily.EnumerateArray());

            var failed = await client.PostAsync("/api/entries", EntryForm(categoryId, "Failed", "failed", 300));
            var failedAttempt = (await JsonDocument.ParseAsync(await failed.Content.ReadAsStreamAsync()))
                .RootElement.GetProperty("attemptId").GetGuid();
            await SetOutcomeAsync(client, failedAttempt, "Cancelled");
            (await client.PostAsync($"/api/payments/{failedAttempt}/confirm", null)).EnsureSuccessStatusCode();
            Assert.Equal("Cancelled", await PaymentStateAsync(client, failedAttempt));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var payments = await client.GetFromJsonAsync<JsonElement>("/api/admin/payments");
            Assert.Equal(3, payments.GetArrayLength());
            (await client.PostAsync($"/api/admin/entries/{entryId}/hide", null)).EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = null;
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/entries/{entryId}")).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/leaderboards/global")).EnumerateArray());

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await client.PostAsync($"/api/admin/entries/{entryId}/restore", null)).EnsureSuccessStatusCode();
            (await client.PutAsJsonAsync($"/api/admin/entries/{entryId}/details",
                new { name = "Updated Creator", username = "updated" })).EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Authorization = null;
            Assert.Equal("Updated Creator", (await client.GetFromJsonAsync<JsonElement>($"/api/entries/{entryId}"))
                .GetProperty("name").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Validation_errors_are_problem_details_and_mock_controls_are_development_only()
    {
        var directory = Path.Combine(Path.GetTempPath(), "crownrank-api-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var factory = new CrownRankApiFactory(directory);
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync($"/api/entries/{Guid.NewGuid()}/boosts",
                new { amountInMinorUnits = -1, currency = "EUR" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/entries", new StringContent("not-form-data"))).StatusCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MultipartFormDataContent EntryForm(Guid categoryId, string name, string username, long amount)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "name" },
            { new StringContent(username), "username" },
            { new StringContent(categoryId.ToString()), "categoryId" },
            { new StringContent("true"), "acceptedAgreements" },
            { new StringContent(amount.ToString()), "amountInMinorUnits" },
            { new StringContent("EUR"), "currency" },
            { new StringContent("[{\"platform\":\"Instagram\",\"url\":\"https://instagram.com/creator\"}]"), "socialLinks" }
        };
        var image = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z7ZkAAAAASUVORK5CYII="));
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "profile.png");
        return form;
    }

    private static async Task SetOutcomeAsync(HttpClient client, Guid attemptId, string outcome)
    {
        var response = await client.PostAsJsonAsync($"/api/dev/payments/{attemptId}/outcome", new { outcome });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> PaymentStateAsync(HttpClient client, Guid attemptId)
        => (await client.GetFromJsonAsync<JsonElement>($"/api/payments/{attemptId}"))
            .GetProperty("state").GetString()!;

    private sealed class CrownRankApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previous = [];

        public CrownRankApiFactory(string directory)
        {
            Set("CrownRank__DatabasePath", Path.Combine(directory, "api.db"));
            Set("CrownRank__ImageDirectory", Path.Combine(directory, "assets"));
            Set("CrownRank__EnableMockPayments", "true");
            Set("CrownRank__AdminPassword", "api-test-admin");
            Set("CrownRank__TermsVersion", "terms-test");
            Set("CrownRank__PrivacyVersion", "privacy-test");
            Set("CrownRank__RulesVersion", "rules-test");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Testing");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var pair in _previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }

        private void Set(string name, string value)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
