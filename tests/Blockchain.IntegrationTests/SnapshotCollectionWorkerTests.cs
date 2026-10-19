using System.Threading.Channels;
using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Infrastructure;
using Blockchain.Infrastructure.Collection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Blockchain.IntegrationTests;

public sealed class SnapshotCollectionWorkerTests
{
    [Fact]
    public async Task Disabled_worker_does_not_resolve_a_capture_scope()
    {
        await using var harness = new Harness(enabled: false);
        await harness.Worker.StartAsync(CancellationToken.None);
        await harness.Worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, harness.Calls);
        Assert.False(harness.Clock.Timers.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Initial_delay_and_delay_after_partial_success_use_fresh_disposed_scopes()
    {
        await using var harness = new Harness();
        await harness.StartCycleAsync();
        await harness.Disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await harness.Clock.ExpectTimerAsync(600);
        harness.Clock.Advance(599);
        Assert.Equal(1, harness.Calls);
        harness.Clock.Advance(1);
        await harness.Clock.ExpectTimerAsync(120);
        var second = await harness.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await harness.Disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, second);
        Assert.Equal(2, harness.Scopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_failure_or_cycle_timeout_does_not_kill_subsequent_cycles(bool timeout)
    {
        await using var harness = new Harness();
        harness.Capture = async (call, token) =>
        {
            if (call != 1) return;
            if (timeout) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            else throw new InvalidOperationException("Database unavailable");
        };
        await harness.StartCycleAsync();
        if (timeout) harness.Clock.Advance(120);
        await harness.Disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await harness.Clock.ExpectTimerAsync(600);
        harness.Clock.Advance(600);
        await harness.Clock.ExpectTimerAsync(120);
        Assert.Equal(2, await harness.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await harness.Disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Slow_cycles_do_not_overlap_and_shutdown_cancels_active_capture()
    {
        await using var harness = new Harness(intervalSeconds: 1);
        harness.Capture = (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token);
        await harness.StartCycleAsync();
        harness.Clock.Advance(60);
        Assert.Equal(1, harness.Calls);
        Assert.False(harness.Clock.Timers.Reader.TryRead(out _));
        await harness.Worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await harness.Disposed.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(harness.Worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Shutdown_during_initial_delay_never_starts_capture()
    {
        await using var harness = new Harness();
        await harness.Worker.StartAsync(CancellationToken.None);
        await harness.Clock.ExpectTimerAsync(10);
        await harness.Worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        harness.Clock.Advance(1000);
        Assert.Equal(0, harness.Calls);
    }

    [Theory]
    [InlineData("IntervalSeconds", "0")]
    [InlineData("IntervalSeconds", "86401")]
    [InlineData("InitialDelaySeconds", "-1")]
    [InlineData("CycleTimeoutSeconds", "0")]
    public void Invalid_schedule_configuration_is_rejected(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"SnapshotCollection:{key}"] = value
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SnapshotCollectionOptions>>().Value);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        public readonly ObservedClock Clock = new();
        public readonly Channel<int> Started = Channel.CreateUnbounded<int>();
        public readonly Channel<int> Disposed = Channel.CreateUnbounded<int>();
        public int Calls;
        public int Scopes;
        public Func<int, CancellationToken, Task> Capture = (_, _) => Task.CompletedTask;
        public SnapshotCollectionWorker Worker { get; }

        public Harness(bool enabled = true, int intervalSeconds = 600)
        {
            var services = new ServiceCollection();
            services.AddScoped<ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<CaptureResult>>>(
                _ => new Handler(this, Interlocked.Increment(ref Scopes)));
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new SnapshotCollectionOptions
            {
                Enabled = enabled, IntervalSeconds = intervalSeconds
            }), Clock, NullLogger<SnapshotCollectionWorker>.Instance);
        }

        public async Task StartCycleAsync()
        {
            await Worker.StartAsync(CancellationToken.None);
            await Clock.ExpectTimerAsync(10);
            Assert.Equal(0, Calls);
            Clock.Advance(10);
            await Clock.ExpectTimerAsync(120);
            Assert.Equal(1, await Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }

        public async ValueTask DisposeAsync()
        {
            await Worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            Worker.Dispose();
            await provider.DisposeAsync();
        }

        private sealed class Handler(Harness harness, int scopeId)
            : ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<CaptureResult>>, IAsyncDisposable
        {
            public async Task<IReadOnlyList<CaptureResult>> HandleAsync(CaptureAllSnapshotsCommand command, CancellationToken token)
            {
                var call = Interlocked.Increment(ref harness.Calls);
                harness.Started.Writer.TryWrite(call);
                await harness.Capture(call, token);
                return [new("btc", "main", 1, null), new("eth", "main", null, "provider_circuit_open")];
            }

            public ValueTask DisposeAsync()
            {
                harness.Disposed.Writer.TryWrite(scopeId);
                return ValueTask.CompletedTask;
            }
        }
    }

    // Observe timer registration before advancing virtual time, avoiding scheduling races and real sleeps.
    private sealed class ObservedClock : TimeProvider
    {
        private readonly FakeTimeProvider inner = new();
        public Channel<TimeSpan> Timers { get; } = Channel.CreateUnbounded<TimeSpan>();
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            Timers.Writer.TryWrite(dueTime);
            return timer;
        }
        public void Advance(int seconds) => inner.Advance(TimeSpan.FromSeconds(seconds));
        public async Task ExpectTimerAsync(int seconds) => Assert.Equal(TimeSpan.FromSeconds(seconds),
            await Timers.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
