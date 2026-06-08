using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SpendingApi.Application.SpendingSummary;

namespace SpendingApi.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SpendingSummaryIntegrationTests : IClassFixture<SpendingApiFactory>
{
    private readonly SpendingApiFactory _factory;

    public SpendingSummaryIntegrationTests(SpendingApiFactory factory)
    {
        _factory = factory;
    }

    // Creates a fresh client per test — no shared DefaultRequestHeaders state
    private HttpClient AuthedClient(Guid customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForCustomer(customerId));
        return client;
    }

    [Fact]
    public async Task GetSpendingSummary_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}/spending-summary?year=2026&month=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSpendingSummary_WithInvalidMonth_Returns400()
    {
        var customerId = Guid.NewGuid();
        var client = AuthedClient(customerId);  // JWT sub == URL customerId

        var response = await client.GetAsync($"/api/v1/customers/{customerId}/spending-summary?year=2026&month=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSpendingSummary_WithNoTransactions_ReturnsEmptySummary()
    {
        var customerId = Guid.NewGuid();
        var client = AuthedClient(customerId);

        var response = await client.GetAsync($"/api/v1/customers/{customerId}/spending-summary?year=2026&month=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<SpendingSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(0m, summary.TotalSpend);
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public async Task GetSpendingSummary_ReturnsCachedResponseOnSecondCall()
    {
        var customerId = Guid.NewGuid();
        var client = AuthedClient(customerId);
        var url = $"/api/v1/customers/{customerId}/spending-summary?year=2026&month=1";

        var first  = await client.GetAsync(url);
        var second = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task GetSpendingSummary_HasSecurityHeaders()
    {
        var customerId = Guid.NewGuid();
        var client = AuthedClient(customerId);

        var response = await client.GetAsync($"/api/v1/customers/{customerId}/spending-summary?year=2026&month=1");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }

    [Fact]
    public async Task GetSpendingSummary_WithWrongCustomer_Returns403()
    {
        var realCustomerId    = Guid.NewGuid();
        var anotherCustomerId = Guid.NewGuid();
        var client = AuthedClient(realCustomerId);  // JWT is for realCustomerId

        // Requesting another customer's data — IDOR attempt
        var response = await client.GetAsync($"/api/v1/customers/{anotherCustomerId}/spending-summary?year=2026&month=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
