using System.Net;
using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Blockchain.IntegrationTests;

public sealed class ProviderCooldownTests
{
    [Fact]
    public async Task Cooldown_blocks_waiting_callers_and_does_not_consume_hourly_quota()
    {
        var clock = new FakeTimeProvider();
        using var limiter = new ProviderRequestLimiter(Options.Create(new BlockCypherOptions
        { RequestsPerHour = 2, RequestsPerSecond = 1 }), clock);
        await limiter.WaitForPermitAsync(default);
        var waiting = limiter.WaitForPermitAsync(default);
        limiter.ApplyCooldown(TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<ProviderException>(() => waiting);
        await Assert.ThrowsAsync<ProviderException>(() => limiter.WaitForPermitAsync(default));
        limiter.ApplyCooldown(TimeSpan.FromSeconds(1)); // A shorter response cannot shorten an existing pause.
        clock.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ProviderException>(() => limiter.WaitForPermitAsync(default));
        clock.Advance(TimeSpan.FromSeconds(7));
        await limiter.WaitForPermitAsync(default); // Rejections consumed no permits.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Last_429_attempt_blocks_other_clients_until_retry_after_expires(bool dateHeader)
    {
        var clock = new FakeTimeProvider();
        var options = Options.Create(new BlockCypherOptions { MaxRetries = 0 });
        using var limiter = new ProviderRequestLimiter(options, clock);
        var circuit = new ProviderCircuitBreaker(options, clock, NullLogger<ProviderCircuitBreaker>.Instance);
        var handler = new ThrottledHandler(clock, dateHeader);
        using var http = new HttpClient(handler) { BaseAddress = new("https://example.invalid/") };
        BlockCypherClient CreateClient() => new(http, limiter, circuit, options, clock, NullLogger<BlockCypherClient>.Instance);
        var network = BlockchainNetwork.FindSupportedNetwork("btc", "main")!;
        await Assert.ThrowsAsync<ProviderException>(() => CreateClient().FetchSnapshotAsync(network, default));
        await Assert.ThrowsAsync<ProviderException>(() => CreateClient().FetchSnapshotAsync(network, default));
        Assert.Equal(1, handler.Calls);
        clock.Advance(TimeSpan.FromSeconds(30));
        var snapshot = await CreateClient().FetchSnapshotAsync(network, default);
        Assert.Equal("{\"name\":\"BTC.main\"}", snapshot.Payload);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_takes_precedence_over_cooldown()
    {
        using var limiter = new ProviderRequestLimiter(Options.Create(new BlockCypherOptions()), new FakeTimeProvider());
        limiter.ApplyCooldown(TimeSpan.FromMinutes(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.WaitForPermitAsync(cancellation.Token));
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
