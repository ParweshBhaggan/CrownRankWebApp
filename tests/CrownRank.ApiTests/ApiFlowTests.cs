using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CrownRank.ApiTests;

public sealed class ApiFlowTests
{
    [Fact]
    public async Task Complete_entry_rank_boost_and_hide_flow_obeys_the_public_contract()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var entryReference = Guid.NewGuid();

        using var entryResponse = await client.PostAsync("/api/creators", ValidEntry(entryReference));
        Assert.Equal(HttpStatusCode.Created, entryResponse.StatusCode);
        var created = await entryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var creatorId = created.GetProperty("id").GetGuid();
        Assert.Equal(12.50m, created.GetProperty("totalContributed").GetDecimal());
        Assert.Equal("ada", created.GetProperty("username").GetString());

        var global = await client.GetFromJsonAsync<JsonElement[]>("/api/creators");
        Assert.Single(global!);
        var daily = await client.GetFromJsonAsync<JsonElement[]>("/api/rankings/daily/2026-09-08");
        Assert.Single(daily!);

        var boostReference = Guid.NewGuid();
        var boost = new { referenceId = boostReference, creatorId, purpose = "creator-boost", amount = 2.50m, currency = "USD" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments/checkout", boost)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/payments/checkout", boost)).StatusCode);
        global = await client.GetFromJsonAsync<JsonElement[]>("/api/creators");
        Assert.Equal(15m, global![0].GetProperty("totalContributed").GetDecimal());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/creators/{creatorId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/creators/{creatorId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/creators"))!);
        Assert.Equal(2, factory.Store.Creators.Single().Contributions.Count);
    }

    [Fact]
    public async Task Exact_entry_retry_is_idempotent_and_duplicate_username_conflicts()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var reference = Guid.NewGuid();

        var first = await client.PostAsync("/api/creators", ValidEntry(reference));
        var retry = await client.PostAsync("/api/creators", ValidEntry(reference));
        var duplicate = await client.PostAsync("/api/creators", ValidEntry(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Single(factory.Store.Creators);
        Assert.Single(factory.Store.Creators.Single().Contributions);
    }

    [Fact]
    public async Task Invalid_entry_and_checkout_inputs_return_problem_details()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();

        using var invalidEntry = ValidEntry(Guid.Empty);
        var entryResponse = await client.PostAsync("/api/creators", invalidEntry);
        Assert.Equal(HttpStatusCode.BadRequest, entryResponse.StatusCode);
        Assert.Equal("application/problem+json", entryResponse.Content.Headers.ContentType?.MediaType);

        var checkoutResponse = await client.PostAsJsonAsync("/api/payments/checkout", new
        {
            referenceId = Guid.NewGuid(), creatorId = Guid.NewGuid(), purpose = "creator-boost", amount = 1.001m, currency = "USD"
        });
        Assert.Equal(HttpStatusCode.BadRequest, checkoutResponse.StatusCode);
    }

    [Fact]
    public async Task Missing_creator_returns_not_found_for_read_delete_and_boost()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateHttpsClient();
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/creators/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/creators/{missing}")).StatusCode);
        var boost = await client.PostAsJsonAsync("/api/payments/checkout", new
        {
            referenceId = Guid.NewGuid(), creatorId = missing, purpose = "creator-boost", amount = 10m, currency = "USD"
        });
        Assert.Equal(HttpStatusCode.NotFound, boost.StatusCode);
    }

    [Fact]
    public async Task Mutation_and_mock_payment_routes_are_not_exposed_outside_development()
    {
        await using var factory = new TestApiFactory(Environments.Production);
        using var client = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/api/creators", ValidEntry(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/payments/checkout", new { })).StatusCode);
    }

    private static MultipartFormDataContent ValidEntry(Guid reference)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(reference.ToString()), "entryReference");
        content.Add(new StringContent("Ada"), "firstName");
        content.Add(new StringContent("Lovelace"), "lastName");
        content.Add(new StringContent("Ada"), "username");
        content.Add(new StringContent("technology"), "category");
        content.Add(new StringContent("12.50"), "initialAmount");
        content.Add(new StringContent("[{\"platform\":\"instagram\",\"url\":\"https://instagram.com/ada\"}]"), "socialProfilesJson");
        return content;
    }
}
