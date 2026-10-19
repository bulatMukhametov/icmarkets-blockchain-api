using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Domain;
using Blockchain.Tests.Shared;

namespace Blockchain.UnitTests;

public sealed class BatchConcurrencyTests
{
    [Fact]
    public async Task A_larger_catalog_keeps_at_most_four_captures_active()
    {
        var currency = new Cryptocurrency(Guid.NewGuid(), "btc", "Bitcoin");
        var catalog = new TestNetworkCatalog
        {
            Entries = Enumerable.Range(1, 20).Select(index =>
                new BlockchainNetwork(Guid.NewGuid(), currency, $"testnet{index}", NetworkKind.Testnet)).ToArray()
        };
        var capture = new BlockingCapture();
        var batch = new CaptureAllSnapshotsHandler(capture, catalog).HandleAsync(new(), default);
        try
        {
            await capture.FourStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(4, Volatile.Read(ref capture.Calls));
        }
        finally { capture.Release.TrySetResult(); }
        var results = await batch.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, results.Count);
        Assert.Equal(20, capture.Calls);
    }

    private sealed class BlockingCapture : ISnapshotCaptureService
    {
        public TaskCompletionSource FourStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        private int active;

        public async Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken token)
        {
            var concurrent = Interlocked.Increment(ref active);
            Assert.InRange(concurrent, 1, 4);
            if (Interlocked.Increment(ref Calls) == 4) FourStarted.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(token);
                return new(network, DateTimeOffset.UtcNow, "{}");
            }
            finally { Interlocked.Decrement(ref active); }
        }
    }
}
