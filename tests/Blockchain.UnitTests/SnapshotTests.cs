using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Application.Snapshots.GetSnapshotHistory;
using Blockchain.Domain;

namespace Blockchain.UnitTests;

public sealed class SnapshotTests
{
    private static readonly ChainId Btc = ChainId.Find("btc", "main")!;

    [Fact]
    public void Chain_catalog_distinguishes_networks_and_rejects_invalid_combinations()
    {
        Assert.Equal(5, ChainId.Supported.Count);
        Assert.Equal(Btc, ChainId.Find("BTC", "MAIN"));
        Assert.NotEqual(Btc, ChainId.Find("btc", "test3"));
        Assert.Null(ChainId.Find("eth", "test3"));
    }

    [Fact]
    public async Task Capture_preserves_complete_payload_and_provider_request_time()
    {
        const string json = "{ \"name\":\"BTC.main\", \"unknown\": [12345678901234567890, {\"x\":true}] }";
        var time = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(19);
        var repository = new StubRepository();
        var handler = new CaptureSnapshotHandler(new StubClient(new(json, time)), repository);
        var first = await handler.HandleAsync(new(Btc), default);
        await handler.HandleAsync(new(Btc), default);
        Assert.Equal(json, first.Payload);
        Assert.Equal(time.UtcTicks - time.UtcTicks % 10, first.CreatedAt.Ticks);
        Assert.Equal(TimeSpan.Zero, first.CreatedAt.Offset);
        Assert.Equal(2, repository.Added.Count); // Equal payloads are still separate observations.
    }

    [Fact]
    public async Task Provider_failure_does_not_write_a_snapshot()
    {
        var repository = new StubRepository();
        var handler = new CaptureSnapshotHandler(new StubClient(error: new ProviderException("offline")), repository);
        await Assert.ThrowsAsync<ProviderException>(() => handler.HandleAsync(new(Btc), default));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task History_validates_page_size_and_cursor_before_accessing_repository()
    {
        var repository = new StubRepository();
        var handler = new GetSnapshotHistoryHandler(repository);
        await Assert.ThrowsAsync<RequestValidationException>(() => handler.HandleAsync(new(Btc, 101), default));
        await Assert.ThrowsAsync<RequestValidationException>(() => handler.HandleAsync(new(Btc, 20, "bad!"), default));
        Assert.Equal(0, repository.ReadCount);
    }

    [Fact]
    public async Task Collection_reports_partial_success_and_still_attempts_all_sources()
    {
        var capture = new StubCapture();
        var results = await new CaptureAllSnapshotsHandler(capture).HandleAsync(new(), default);
        Assert.Equal(5, results.Count);
        Assert.Equal(4, results.Count(x => x.Succeeded));
        Assert.Equal("provider_timeout", Assert.Single(results, x => !x.Succeeded).Error);
    }

    [Fact]
    public async Task Collection_propagates_caller_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CaptureAllSnapshotsHandler(new StubCapture()).HandleAsync(new(), source.Token));
    }

    [Fact]
    public async Task Collection_does_not_hide_persistence_failures()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CaptureAllSnapshotsHandler(new BrokenCapture()).HandleAsync(new(), default));
    }

    private sealed class StubClient(ProviderSnapshot? value = null, Exception? error = null) : IBlockchainDataClient
    {
        public Task<ProviderSnapshot> FetchAsync(ChainId chain, CancellationToken cancellationToken) =>
            error is null ? Task.FromResult(value!) : Task.FromException<ProviderSnapshot>(error);
    }

    private sealed class StubCapture : ISnapshotCapture
    {
        public Task<BlockchainSnapshot> CaptureAsync(ChainId chain, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return chain.Coin == "eth"
                ? Task.FromException<BlockchainSnapshot>(new ProviderException("timeout", timedOut: true))
                : Task.FromResult(new BlockchainSnapshot(chain, DateTimeOffset.UtcNow, "{}"));
        }
    }

    private sealed class BrokenCapture : ISnapshotCapture
    {
        public Task<BlockchainSnapshot> CaptureAsync(ChainId chain, CancellationToken cancellationToken) =>
            Task.FromException<BlockchainSnapshot>(new InvalidOperationException("database down"));
    }

    private sealed class StubRepository : IBlockchainSnapshotRepository
    {
        public List<BlockchainSnapshot> Added { get; } = [];
        public int ReadCount { get; private set; }
        public Task AddAsync(BlockchainSnapshot snapshot, CancellationToken cancellationToken)
        {
            Added.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<BlockchainSnapshot?> GetByIdAsync(ChainId chain, long id, CancellationToken cancellationToken) =>
            Task.FromResult<BlockchainSnapshot?>(null);
        public Task<IReadOnlyList<BlockchainSnapshot>> GetHistoryAsync(ChainId chain, SnapshotCursor? before,
            int take, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<BlockchainSnapshot>>(Added.Take(take).ToArray());
        }
    }
}
