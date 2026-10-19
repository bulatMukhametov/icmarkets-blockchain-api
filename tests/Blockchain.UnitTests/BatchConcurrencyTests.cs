using Blockchain.Application.Snapshots;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Domain;
using Blockchain.Tests.Shared;

namespace Blockchain.UnitTests;

public sealed class BatchConcurrencyTests
{
    [Fact]
    public async Task Batch_starts_all_captures_concurrently_and_returns_results_in_catalog_order()
    {
        var currency = new Cryptocurrency(Guid.NewGuid(), "btc", "Bitcoin");
        var catalog = new TestNetworkCatalog
        {
            Entries = Enumerable.Range(1, 5).Select(index =>
                new BlockchainNetwork(Guid.NewGuid(), currency, $"testnet{index}", NetworkKind.Testnet)).ToArray()
        };
        var capture = new BlockingCapture();
        var batch = new CaptureAllSnapshotsHandler(capture, catalog).HandleAsync(new(), default);
        try
        {
            await capture.AllStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(5, Volatile.Read(ref capture.Calls));
            Assert.False(batch.IsCompleted);
        }
        finally { capture.Release.TrySetResult(); }
        var results = await batch.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(5, results.Count);
        Assert.Equal(catalog.Entries.Select(network => network.Code), results.Select(result => result.NetworkCode));
    }

    private sealed class BlockingCapture : ISnapshotCaptureService
    {
        public TaskCompletionSource AllStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;

        public async Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken token)
        {
            if (Interlocked.Increment(ref Calls) == 5) AllStarted.TrySetResult();
            await Release.Task.WaitAsync(token);
            return new(network, DateTimeOffset.UtcNow, "{}");
        }
    }
}
