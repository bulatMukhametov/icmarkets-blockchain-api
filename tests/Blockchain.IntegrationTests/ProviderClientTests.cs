using System.Net;
using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blockchain.IntegrationTests;

public sealed class ProviderClientTests
{
    private static readonly ChainId Btc = ChainId.Find("btc", "main")!;

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"name\":\"ETH.main\"}")]
    public async Task Rejects_invalid_or_wrong_chain_payload(string payload)
    {
        using var harness = new ClientHarness(_ => new(HttpStatusCode.OK) { Content = new StringContent(payload) });
        await Assert.ThrowsAsync<ProviderException>(() => harness.Client.FetchAsync(Btc, default));
    }

    [Fact]
    public async Task Retries_transient_errors_and_preserves_raw_json()
    {
        const string json = "{ \"name\": \"BTC.main\", \"custom\": 12345678901234567890 }";
        var count = 0;
        using var harness = new ClientHarness(_ => ++count == 1
            ? new(HttpStatusCode.ServiceUnavailable)
            : new(HttpStatusCode.OK) { Content = new StringContent(json) }, retries: 1);
        var result = await harness.Client.FetchAsync(Btc, default);
        Assert.Equal(2, count);
        Assert.Equal(json, result.Payload);
        Assert.True(result.RequestedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Does_not_retry_non_transient_errors()
    {
        var count = 0;
        using var harness = new ClientHarness(_ => { count++; return new(HttpStatusCode.BadRequest); }, retries: 2);
        await Assert.ThrowsAsync<ProviderException>(() => harness.Client.FetchAsync(Btc, default));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Rejects_oversized_response()
    {
        using var harness = new ClientHarness(_ => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2048)) });
        await Assert.ThrowsAsync<ProviderException>(() => harness.Client.FetchAsync(Btc, default));
    }

    [Fact]
    public async Task Hourly_budget_is_shared_by_all_attempts()
    {
        var settings = Options.Create(new BlockCypherOptions { RequestsPerHour = 1, RequestsPerSecond = 3 });
        using var gate = new ProviderRequestGate(settings, TimeProvider.System);
        await gate.WaitAsync(default);
        await Assert.ThrowsAsync<ProviderException>(() => gate.WaitAsync(default));
    }

    [Fact]
    public async Task Long_retry_after_fails_promptly_without_an_early_retry()
    {
        var count = 0;
        using var harness = new ClientHarness(_ =>
        {
            count++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromHours(1));
            return response;
        }, retries: 1);
        await Assert.ThrowsAsync<ProviderException>(() => harness.Client.FetchAsync(Btc, default));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Timeout_is_reported_as_provider_timeout()
    {
        var settings = Options.Create(new BlockCypherOptions { AttemptTimeoutSeconds = 1, MaxRetries = 0 });
        using var http = new HttpClient(new SlowHandler()) { BaseAddress = new("https://example.invalid/") };
        using var gate = new ProviderRequestGate(settings, TimeProvider.System);
        var client = new BlockCypherClient(http, gate, settings, TimeProvider.System, NullLogger<BlockCypherClient>.Instance);
        var error = await Assert.ThrowsAsync<ProviderException>(() => client.FetchAsync(Btc, default));
        Assert.True(error.TimedOut);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_provider_failure()
    {
        using var harness = new ClientHarness(_ => new(HttpStatusCode.OK));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Client.FetchAsync(Btc, source.Token));
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(HttpStatusCode.OK);
        }
    }

    private sealed class ClientHarness : IDisposable
    {
        private readonly HttpClient http;
        private readonly ProviderRequestGate gate;
        public BlockCypherClient Client { get; }
        public ClientHarness(Func<HttpRequestMessage, HttpResponseMessage> respond, int retries = 0)
        {
            var settings = Options.Create(new BlockCypherOptions
            { RequestsPerSecond = 3, MaxRetries = retries, MaxResponseBytes = 1024 });
            http = new(new ResponseHandler(respond)) { BaseAddress = new("https://example.invalid/v1/") };
            gate = new(settings, TimeProvider.System);
            Client = new(http, gate, settings, TimeProvider.System, NullLogger<BlockCypherClient>.Instance);
        }
        public void Dispose() { http.Dispose(); gate.Dispose(); }
    }
}
