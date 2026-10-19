using System.Net;
using Blockchain.Application.Providers;
using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Blockchain.IntegrationTests;

public sealed class ProviderThrottlingTests
{
    [Fact]
    public async Task Hourly_quota_is_available_again_after_the_window_expires()
    {
        var clock = new FakeTimeProvider();
        using var limiter = new ProviderRequestLimiter(Options.Create(new BlockCypherOptions
        { RequestsPerHour = 1, RequestsPerSecond = 1 }), clock);
        await limiter.WaitForPermitAsync(default);
        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ProviderException>(() => limiter.WaitForPermitAsync(default));
        clock.Advance(TimeSpan.FromHours(1));
        await limiter.WaitForPermitAsync(default);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Throttling_is_not_retried_and_does_not_pause_other_captures(bool dateHeader)
    {
        var clock = new FakeTimeProvider();
        var options = Options.Create(new BlockCypherOptions { MaxRetries = 2 });
        using var limiter = new ProviderRequestLimiter(options, clock);
        var circuit = new ProviderCircuitBreaker(options, clock, NullLogger<ProviderCircuitBreaker>.Instance);
        var handler = new ThrottledHandler(clock, dateHeader);
        using var http = new HttpClient(handler) { BaseAddress = new("https://example.invalid/") };
        BlockCypherClient CreateClient() => new(http, limiter, circuit, new Blockchain.Tests.Shared.TestBlockCypherMappings(), options, clock, NullLogger<BlockCypherClient>.Instance);
        var network = Blockchain.Tests.Shared.TestNetworkCatalog.Find("btc", "main")!;
        await Assert.ThrowsAsync<ProviderException>(() => CreateClient().FetchSnapshotAsync(network, default));
        Assert.Equal(1, handler.Calls);
        clock.Advance(TimeSpan.FromSeconds(1));
        var snapshot = await CreateClient().FetchSnapshotAsync(network, default);
        Assert.Equal("{\"name\":\"BTC.main\"}", snapshot.RawResponseJson);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_consume_a_quota_slot()
    {
        using var limiter = new ProviderRequestLimiter(Options.Create(new BlockCypherOptions()), new FakeTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.WaitForPermitAsync(cancellation.Token));
        await limiter.WaitForPermitAsync(default);
    }

    private sealed class ThrottledHandler(TimeProvider clock, bool dateHeader) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (++Calls > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"name\":\"BTC.main\"}") });
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = dateHeader
                ? new(clock.GetUtcNow().AddSeconds(30)) : new(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        }
    }
}
