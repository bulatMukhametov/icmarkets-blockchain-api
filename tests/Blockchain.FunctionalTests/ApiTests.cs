using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Blockchain.Api.Contracts;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;

namespace Blockchain.FunctionalTests;

public sealed class ApiTests
{
    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    [InlineData("application/json")]
    public async Task Validation_errors_keep_details_and_trace_id_for_any_accept_header(string accept)
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/chains/btc/main/snapshots?page=0&pageSize=101");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.RootElement.GetProperty("errors").GetProperty("page").EnumerateArray());
        Assert.NotEmpty(body.RootElement.GetProperty("errors").GetProperty("pageSize").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(0, factory.ProviderRequests);
    }

    [Fact]
    public async Task Enabled_worker_populates_all_chain_histories_without_a_capture_request()
    {
        await using var factory = new ApiFactory(collectionEnabled: true);
        using var http = factory.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var network in Blockchain.Tests.Shared.TestNetworkCatalog.Networks)
        {
            SnapshotHistoryResponse? history;
            do
            {
                history = await http.GetFromJsonAsync<SnapshotHistoryResponse>(
                    $"/api/chains/{network.Cryptocurrency.Code}/{network.Code}/snapshots", deadline.Token);
                if (history!.Items.Count == 0) await Task.Delay(25, deadline.Token);
            } while (history.Items.Count == 0);
            Assert.Single(history.Items);
        }
        Assert.Equal(5, factory.ProviderRequests);
    }

    [Fact]
    public async Task Open_circuit_returns_503_and_retry_after_while_history_and_batch_contract_remain_available()
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var created = await http.PostAsync("/api/chains/btc/main/snapshots", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        factory.FailingCoin = "btc";
        for (var i = 0; i < 4; i++)
        {
            using var failure = await http.PostAsync("/api/chains/btc/main/snapshots", null);
            Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        }
        using var blocked = await http.PostAsync("/api/chains/eth/main/snapshots", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.InRange(blocked.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 30);
        Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);
        var history = await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/btc/main/snapshots");
        Assert.Single(history!.Items);
        using var collection = await http.PostAsync("/api/snapshot-collections", null);
        Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
        var results = (await collection.Content.ReadFromJsonAsync<NetworkCaptureResponse[]>())!;
        Assert.Equal(5, results.Length);
        Assert.All(results, result => { Assert.False(result.Succeeded); Assert.Equal("provider_circuit_open", result.Error); });
        Assert.Equal(5, factory.ProviderRequests);
    }

    [Fact]
    public async Task Capture_returns_location_raw_payload_and_readable_history_without_refetching()
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var created = await http.PostAsync("/api/chains/btc/main/snapshots", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var snapshot = (await created.Content.ReadFromJsonAsync<SnapshotResponse>())!;
        Assert.Equal(JsonValueKind.Object, snapshot.Payload.ValueKind);
        Assert.Equal("12345678901234567890", snapshot.Payload.GetProperty("unknown").GetProperty("large").GetRawText());
        using var get = await http.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var history = await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/btc/main/snapshots");
        Assert.Equal(snapshot.Id, Assert.Single(history!.Items).Id);
        Assert.Equal(1, factory.ProviderRequests);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync($"/api/chains/btc/test3/snapshots/{snapshot.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/chains/eth/test3/snapshots")]
    [InlineData("/api/chains/btc/main/snapshots?pageSize=0")]
    [InlineData("/api/chains/btc/main/snapshots?pageSize=101")]
    [InlineData("/api/chains/btc/main/snapshots?page=0")]
    [InlineData("/api/chains/btc/main/snapshots/0")]
    public async Task Invalid_requests_return_validation_problem_details(string url)
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        using var response = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(0, factory.ProviderRequests);
    }

    [Fact]
    public async Task Provider_failure_returns_502_and_does_not_create_history()
    {
        await using var factory = new ApiFactory { FailingCoin = "btc" };
        using var http = factory.CreateClient();
        using var response = await http.PostAsync("/api/chains/btc/main/snapshots", null);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var page = await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/btc/main/snapshots");
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Collection_keeps_successful_sources_when_one_provider_fails()
    {
        await using var factory = new ApiFactory { FailingCoin = "eth" };
        using var http = factory.CreateClient();
        using var response = await http.PostAsync("/api/snapshot-collections", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<NetworkCaptureResponse[]>())!;
        Assert.Equal(5, results.Length);
        Assert.Equal(4, results.Count(x => x.Succeeded));
        Assert.Equal("provider_unavailable", Assert.Single(results, x => !x.Succeeded).Error);
        Assert.Equal(5, factory.ProviderRequests);
    }

    [Fact]
    public async Task Health_cors_and_swagger_are_exposed()
    {
        await using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await http.GetAsync("/health/ready")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/chains");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await http.SendAsync(request);
        Assert.Equal("http://localhost:5173", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        using var swagger = JsonDocument.Parse(await http.GetStringAsync("/swagger/v1/swagger.json"));
        Assert.True(swagger.RootElement.GetProperty("paths").TryGetProperty("/api/snapshot-collections", out _));
        Assert.Equal(0, factory.ProviderRequests);
    }
}
