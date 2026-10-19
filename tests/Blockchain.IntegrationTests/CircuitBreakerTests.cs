using System.Net;
using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Blockchain.IntegrationTests;

public sealed class CircuitBreakerTests
{
    [Fact]
    public async Task Default_threshold_requires_five_attempts_and_counts_failure_ratio()
    {
        using var h = new Harness(new BlockCypherOptions { MaxRetries = 0 });
        foreach (var status in new[] { 503, 200, 503, 200, 503 })
        {
            h.Respond = (_, _) => Task.FromResult(Response(status));
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            if (status == 200) await h.Fetch();
            else await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        }
        var error = await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(TimeSpan.FromSeconds(30), error.RetryAfter);
        Assert.Equal(5, h.Calls);
    }

    [Fact]
    public async Task Old_failures_expire_from_the_sampling_window()
    {
        using var h = new Harness();
        await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        h.Clock.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch();
        Assert.Equal(3, h.Calls);
    }

    [Fact]
    public async Task Open_circuit_is_shared_across_clients_and_chains_without_consuming_quota()
    {
        using var h = new Harness(new BlockCypherOptions
        { MaxRetries = 0, CircuitBreakerMinimumThroughput = 2, RequestsPerHour = 3 });
        await h.Open();
        var otherClient = h.CreateClient();
        for (var i = 0; i < 10; i++)
            await Assert.ThrowsAsync<ProviderCircuitOpenException>(() =>
                otherClient.FetchSnapshotAsync(BlockchainNetwork.FindSupportedNetwork("eth", "main")!, default));
        Assert.Equal(2, h.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch(); // The third quota slot is still available for the recovery probe.
        Assert.Equal(3, h.Calls);
    }

    [Fact]
    public async Task Half_open_allows_one_probe_then_success_restores_requests()
    {
        using var h = new Harness();
        await h.Open();
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Respond = (_, token) => { entered.TrySetResult(); return release.Task.WaitAsync(token); };
        var probe = h.Fetch();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, h.Calls);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.CreateClient().FetchSnapshotAsync(Harness.Btc, default))));
        Assert.Equal(3, h.Calls);
        release.SetResult(Response(200));
        await probe.WaitAsync(TimeSpan.FromSeconds(5));
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch();
        Assert.Equal(4, h.Calls);
    }

    [Fact]
    public async Task Failed_probe_reopens_for_the_full_break_duration()
    {
        using var h = new Harness();
        await h.Open();
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        h.Clock.Advance(TimeSpan.FromSeconds(29));
        var error = await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(TimeSpan.FromSeconds(1), error.RetryAfter);
        Assert.Equal(3, h.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch();
    }

    [Fact]
    public async Task Request_waiting_at_the_gate_rechecks_the_circuit_before_sending()
    {
        using var h = new Harness();
        await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        var release = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Respond = (_, token) => release.Task.WaitAsync(token);
        var second = h.Fetch();
        var queued = h.Fetch();
        Assert.Equal(2, h.Calls);
        release.SetResult(Response(503));
        await Assert.ThrowsAsync<ProviderException>(() => second);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => queued.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task Retries_are_counted_and_stop_when_the_circuit_opens()
    {
        using var h = new Harness(new BlockCypherOptions { MaxRetries = 2, CircuitBreakerMinimumThroughput = 2 });
        var request = h.Fetch();
        Assert.Equal(1, h.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, h.Calls); // The configured third attempt is suppressed.
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(429)]
    public async Task Client_errors_and_throttling_do_not_trip_the_circuit(int status)
    {
        using var h = new Harness();
        h.Respond = (_, _) => Task.FromResult(Response(status));
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        }
        Assert.Equal(3, h.Calls);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Availability_errors_trip_the_circuit(int status)
    {
        using var h = new Harness();
        h.Respond = (_, _) => Task.FromResult(Response(status));
        await h.Open();
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task Network_errors_trip_the_circuit()
    {
        using var h = new Harness();
        h.Respond = (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
        await h.Open();
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task Body_read_failures_trip_the_circuit()
    {
        using var h = new Harness();
        h.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new BrokenStream()) });
        await h.Open();
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task Attempt_timeouts_trip_the_circuit()
    {
        using var h = new Harness(new BlockCypherOptions
        { MaxRetries = 0, AttemptTimeoutSeconds = 1, CircuitBreakerMinimumThroughput = 2 });
        h.Respond = async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Response(200); };
        for (var i = 0; i < 2; i++)
        {
            var request = h.Fetch();
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            var error = await Assert.ThrowsAsync<ProviderException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(error.TimedOut);
        }
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_trip_a_closed_circuit()
    {
        using var h = new Harness();
        h.Respond = async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Response(200); };
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            using var cancel = new CancellationTokenSource();
            var request = h.Client.FetchSnapshotAsync(Harness.Btc, cancel.Token);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        }
        Assert.Equal(3, h.Calls);
    }

    [Fact]
    public async Task Cancelled_probe_does_not_report_recovery_or_leave_half_open_stuck()
    {
        using var h = new Harness();
        await h.Open();
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Respond = async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response(200);
        };
        using var cancel = new CancellationTokenSource();
        var probe = h.Client.FetchSnapshotAsync(Harness.Btc, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
        await Assert.ThrowsAsync<ProviderCircuitOpenException>(() => h.Fetch());
        h.Clock.Advance(TimeSpan.FromSeconds(30));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch();
        Assert.Equal(4, h.Calls);
    }

    [Fact]
    public async Task Invalid_payloads_do_not_trip_the_availability_circuit()
    {
        using var h = new Harness();
        h.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("not-json") });
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        }
        Assert.Equal(3, h.Calls);
    }

    [Fact]
    public async Task Local_quota_exhaustion_does_not_trip_the_provider_circuit()
    {
        using var h = new Harness(new BlockCypherOptions
        { MaxRetries = 0, CircuitBreakerMinimumThroughput = 2, RequestsPerHour = 1 });
        await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<ProviderException>(() => h.Fetch());
        }
        Assert.Equal(1, h.Calls);
        h.Clock.Advance(TimeSpan.FromHours(1));
        h.Respond = (_, _) => Task.FromResult(Response(200));
        await h.Fetch();
        Assert.Equal(2, h.Calls);
    }

    private static HttpResponseMessage Response(int status) => new((HttpStatusCode)status)
    { Content = new StringContent("{\"name\":\"BTC.main\"}") };

    private sealed class BrokenStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Connection lost while reading body"));
    }

    private sealed class Harness : IDisposable
    {
        public static readonly BlockchainNetwork Btc = BlockchainNetwork.FindSupportedNetwork("btc", "main")!;
        public FakeTimeProvider Clock { get; } = new();
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(Response(503));
        public int Calls;
        public BlockCypherClient Client { get; }
        private readonly IOptions<BlockCypherOptions> options;
        private readonly ProviderRequestLimiter gate;
        private readonly ProviderCircuitBreaker circuit;
        private readonly HttpClient http;

        public Harness(BlockCypherOptions? settings = null)
        {
            options = Options.Create(settings ?? new BlockCypherOptions { MaxRetries = 0, CircuitBreakerMinimumThroughput = 2 });
            gate = new(options, Clock);
            circuit = new(options, Clock, NullLogger<ProviderCircuitBreaker>.Instance);
            http = new(new Handler(this)) { BaseAddress = new("https://example.invalid/v1/"), Timeout = Timeout.InfiniteTimeSpan };
            Client = CreateClient();
        }

        public BlockCypherClient CreateClient() => new(http, gate, circuit, options, Clock, NullLogger<BlockCypherClient>.Instance);
        public Task<ProviderSnapshot> Fetch() => Client.FetchSnapshotAsync(Btc, default);
        public async Task Open()
        {
            for (var i = 0; i < 2; i++)
            {
                Clock.Advance(TimeSpan.FromSeconds(1));
                await Assert.ThrowsAsync<ProviderException>(() => Fetch());
            }
        }
        public void Dispose() { http.Dispose(); gate.Dispose(); }

        private sealed class Handler(Harness owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Interlocked.Increment(ref owner.Calls);
                return owner.Respond(request, token);
            }
        }
    }
}
