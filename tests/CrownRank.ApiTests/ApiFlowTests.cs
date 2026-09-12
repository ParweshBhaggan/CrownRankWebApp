using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrownRank.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrownRank.ApiTests;

public sealed class ApiFlowTests
{
    [Fact]
    public async Task Multiple_creators_can_be_created_read_ranked_updated_and_deleted()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        var ada = await CreateAsync(client, "Ada Lovelace", "ada", "technology", 12.50m, ct);
        var grace = await CreateAsync(client, "Grace Hopper", "grace", "education", 30m, ct);
        var linus = await CreateAsync(client, "Linus Torvalds", "linus", "technology", 20m, ct);

        var creators = await client.GetFromJsonAsync<JsonElement[]>("/api/creators", ct);
        Assert.Equal(3, creators!.Length);
        Assert.Equal(grace, creators[0].GetProperty("id").GetGuid());
        Assert.Equal(linus, creators[1].GetProperty("id").GetGuid());
        Assert.Equal(ada, creators[2].GetProperty("id").GetGuid());

        var update = new
        {
            name = "Ada Byron Lovelace", username = "ada", category = "technology",
            socialProfiles = new[] { new { platform = "website", url = "https://example.com/ada" } }
        };
        var updated = await client.PutAsJsonAsync($"/api/creators/{ada}", update, ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Ada Byron Lovelace",
            (await updated.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/creators/{linus}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/creators/{linus}", ct)).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement[]>("/api/creators", ct))!.Length);
    }

    [Fact]
    public async Task Entry_and_boost_retries_are_idempotent()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        var entryReference = Guid.NewGuid();
        using var firstContent = Entry(entryReference, "Ada Lovelace", "ada", 12.50m);
        using var retryContent = Entry(entryReference, "Ada Lovelace", "ada", 12.50m);
        var first = await client.PostAsync("/api/creators", firstContent, ct);
        var retry = await client.PostAsync("/api/creators", retryContent, ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var creatorId = (await first.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("creatorId").GetGuid();
        Assert.Equal(creatorId, (await retry.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("creatorId").GetGuid());

        var boostReference = Guid.NewGuid();
        var boost = new { referenceId = boostReference, creatorId, purpose = "creator-boost", amount = 2.50m, currency = "USD" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments/checkout", boost, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments/checkout", boost, ct)).StatusCode);

        var creator = await client.GetFromJsonAsync<JsonElement>($"/api/creators/{creatorId}", ct);
        Assert.Equal(15m, creator.GetProperty("totalContributed").GetDecimal());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Contributions.Count());
    }

    [Fact]
    public async Task Duplicate_and_invalid_requests_return_problem_details_without_partial_rows()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        await CreateAsync(client, "Ada Lovelace", "ada", "technology", 12.50m, ct);

        using var duplicateContent = Entry(Guid.NewGuid(), "Another Ada", "ada", 10m);
        var duplicate = await client.PostAsync("/api/creators", duplicateContent, ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);

        using var invalidContent = Entry(Guid.NewGuid(), "Invalid", "invalid", 1.001m);
        var invalid = await client.PostAsync("/api/creators", invalidContent, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/creators", ct))!);
    }

    [Fact]
    public async Task Daily_ranking_counts_only_confirmations_for_that_utc_date()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        await CreateAsync(client, "Ada Lovelace", "ada", "technology", 12.50m, ct);

        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/rankings/daily/2026-09-12", ct))!);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/rankings/daily/2026-09-11", ct))!);
    }

    [Fact]
    public async Task Mutations_are_not_exposed_outside_development()
    {
        await using var factory = new TestApiFactory(Environments.Production);
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        using var content = Entry(Guid.NewGuid(), "Ada Lovelace", "ada", 12.50m);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/api/creators", content, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/api/payments/checkout", new { }, ct)).StatusCode);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string name, string username, string category,
        decimal amount, CancellationToken cancellationToken)
    {
        using var content = Entry(Guid.NewGuid(), name, username, amount, category);
        var response = await client.PostAsync("/api/creators", content, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("creatorId").GetGuid();
    }

    private static MultipartFormDataContent Entry(Guid reference, string name, string username, decimal amount,
        string category = "technology")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(reference.ToString()), "entryReference");
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(username), "username");
        content.Add(new StringContent(category), "category");
        content.Add(new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "initialAmount");
        content.Add(new StringContent("[{\"platform\":\"website\",\"url\":\"https://example.com/profile\"}]"),
            "socialProfilesJson");
        return content;
    }
}
