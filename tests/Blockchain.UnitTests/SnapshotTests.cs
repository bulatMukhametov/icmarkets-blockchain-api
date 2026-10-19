using FluentValidation;
using Blockchain.Application.Snapshots.GetSnapshot;
using Blockchain.Application.Providers;
using Blockchain.Application.Snapshots;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Application.Snapshots.GetSnapshotHistory;
using Blockchain.Application.Validation;
using Blockchain.Domain;

namespace Blockchain.UnitTests;

public sealed class SnapshotTests
{
    private static readonly BlockchainNetwork Btc = Blockchain.Tests.Shared.TestNetworkCatalog.Find("btc", "main")!;

    [Fact]
    public async Task Capture_resolves_network_codes_without_caller_loading_an_entity()
    {
        var repository = new StubRepository();
        var handler = new CaptureSnapshotHandler(new StubClient(new("{}")),
            repository, new Blockchain.Tests.Shared.TestNetworkCatalog(), new CaptureSnapshotCommandValidator(), TimeProvider.System);

        var snapshot = await handler.HandleAsync(new("BTC", "MAIN"), default);

        Assert.Equal(Btc.Id, snapshot.BlockchainNetworkId);
        Assert.Single(repository.Added);
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("get")]
    [InlineData("history")]
    public async Task Unknown_network_is_rejected_by_application_before_provider_or_snapshot_storage(string scenario)
    {
        var repository = new StubRepository();
        var catalog = new Blockchain.Tests.Shared.TestNetworkCatalog();
        // A provider call would throw a different exception, making accidental calls visible.
        var client = new StubClient(error: new InvalidOperationException("Provider must not be called."));

        var error = await Assert.ThrowsAsync<RequestValidationException>(async () =>
        {
            switch (scenario)
            {
                case "capture":
                    await new CaptureSnapshotHandler(client, repository, catalog, new CaptureSnapshotCommandValidator(), TimeProvider.System).HandleAsync(new("eth", "test3"), default);
                    break;
                case "get":
                    await new GetSnapshotHandler(repository, catalog, new GetSnapshotQueryValidator())
                        .HandleAsync(new("eth", "test3", 1), default);
                    break;
                default:
                    await new GetSnapshotHistoryHandler(repository, catalog, new GetSnapshotHistoryQueryValidator()).HandleAsync(new("eth", "test3"), default);
                    break;
            }
        });

        Assert.Equal("chain", error.Field);
        Assert.Empty(repository.Added);
        Assert.Equal(0, repository.ReadCount);
    }

    [Fact]
    public void Network_identity_and_kind_are_independent_of_currency_code()
    {
        var currency = new Cryptocurrency(Guid.NewGuid(), " DOGE ", "Dogecoin");
        var main = new BlockchainNetwork(Guid.NewGuid(), currency, "production", NetworkKind.Mainnet);
        var test = new BlockchainNetwork(Guid.NewGuid(), currency, "testnet4", NetworkKind.Testnet);
        Assert.Equal("doge", currency.Code);
        Assert.Equal(currency.Id, main.CryptocurrencyId);
        Assert.Equal(NetworkKind.Mainnet, main.Kind);
        Assert.Equal(NetworkKind.Testnet, test.Kind);
        Assert.NotEqual(main.Id, test.Id);
    }

    [Fact]
    public async Task Capture_records_start_time_before_provider_work_and_preserves_payload()
    {
        const string json = "{ \"name\":\"BTC.main\", \"unknown\": [12345678901234567890, {\"x\":true}] }";
        var time = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(19);
        var clock = new TestClock { UtcNow = time };
        var repository = new StubRepository();
        var handler = new CaptureSnapshotHandler(new StubClient(new(json), onFetch: () => clock.UtcNow = time.AddMinutes(1)), repository, new Blockchain.Tests.Shared.TestNetworkCatalog(), new CaptureSnapshotCommandValidator(), clock);
        var first = await handler.HandleAsync(new("btc", "main"), default);
        await handler.HandleAsync(new("btc", "main"), default);
        Assert.Equal(json, first.RawResponseJson);
        Assert.Equal(time.UtcTicks - time.UtcTicks % 10, first.CreatedAt.Ticks);
        Assert.Equal(TimeSpan.Zero, first.CreatedAt.Offset);
        Assert.Equal(2, repository.Added.Count); // Equal payloads are still separate observations.
    }

    [Fact]
    public async Task Provider_failure_does_not_write_a_snapshot()
    {
        var repository = new StubRepository();
        var handler = new CaptureSnapshotHandler(new StubClient(error: new ProviderException("offline")), repository, new Blockchain.Tests.Shared.TestNetworkCatalog(), new CaptureSnapshotCommandValidator(), TimeProvider.System);
        await Assert.ThrowsAsync<ProviderException>(() => handler.HandleAsync(new("btc", "main"), default));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task History_validates_page_bounds_before_accessing_repository()
    {
        var repository = new StubRepository();
        var handler = new GetSnapshotHistoryHandler(repository, new Blockchain.Tests.Shared.TestNetworkCatalog(), new GetSnapshotHistoryQueryValidator());
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new("btc", "main", PageSize: 101), default));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new("btc", "main", Page: 0), default));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new("btc", "main", int.MaxValue, 100), default));
        Assert.Equal(0, repository.ReadCount);
    }

    [Fact]
    public async Task Collection_reports_partial_success_and_still_attempts_all_sources()
    {
        var capture = new StubCapture();
        var results = await new CaptureAllSnapshotsHandler(capture, new Blockchain.Tests.Shared.TestNetworkCatalog()).HandleAsync(new(), default);
        Assert.Equal(5, results.Count);
        Assert.Equal(4, results.Count(x => x.Succeeded));
        Assert.Equal(SnapshotCaptureFailure.ProviderTimeout, Assert.Single(results, x => !x.Succeeded).Failure);
    }

    [Fact]
    public async Task Collection_propagates_caller_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CaptureAllSnapshotsHandler(new StubCapture(), new Blockchain.Tests.Shared.TestNetworkCatalog()).HandleAsync(new(), source.Token));
    }

    [Fact]
    public async Task Collection_does_not_hide_persistence_failures()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CaptureAllSnapshotsHandler(new BrokenCapture(), new Blockchain.Tests.Shared.TestNetworkCatalog()).HandleAsync(new(), default));
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubClient(ProviderSnapshot? value = null, Exception? error = null, Action? onFetch = null) : IBlockchainDataClient
    {
        public Task<ProviderSnapshot> FetchSnapshotAsync(BlockchainNetwork network, CancellationToken cancellationToken)
        {
            onFetch?.Invoke();
            return error is null ? Task.FromResult(value!) : Task.FromException<ProviderSnapshot>(error);
        }
    }

    private sealed class StubCapture : ISnapshotCaptureService
    {
        public Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return network.Cryptocurrency.Code == "eth"
                ? Task.FromException<BlockchainNetworkSnapshot>(new ProviderException("timeout", timedOut: true))
                : Task.FromResult(new BlockchainNetworkSnapshot(network, DateTimeOffset.UtcNow, "{}"));
        }
    }

    private sealed class BrokenCapture : ISnapshotCaptureService
    {
        public Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken cancellationToken) =>
            Task.FromException<BlockchainNetworkSnapshot>(new InvalidOperationException("database down"));
    }

    private sealed class StubRepository : IBlockchainSnapshotRepository
    {
        public List<BlockchainNetworkSnapshot> Added { get; } = [];
        public int ReadCount { get; private set; }
        public Task SaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken)
        {
            Added.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork network, long id, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<BlockchainNetworkSnapshot?>(null);
        }
        public Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(BlockchainNetwork network, int skip,
            int take, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<BlockchainNetworkSnapshot>>(Added.Skip(skip).Take(take).ToArray());
        }
    }
}
