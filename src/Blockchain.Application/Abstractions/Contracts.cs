using Blockchain.Domain;

namespace Blockchain.Application.Abstractions;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public sealed record ProviderSnapshot(string Payload, DateTimeOffset RequestedAt);

public interface IBlockchainDataClient
{
    Task<ProviderSnapshot> FetchAsync(ChainId chain, CancellationToken cancellationToken);
}

public interface IBlockchainSnapshotRepository
{
    // One append is one transaction. The returned entity has its database-generated ID.
    Task AddAsync(BlockchainSnapshot snapshot, CancellationToken cancellationToken);
    Task<BlockchainSnapshot?> GetByIdAsync(ChainId chain, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BlockchainSnapshot>> GetHistoryAsync(
        ChainId chain, SnapshotCursor? before, int take, CancellationToken cancellationToken);
}

public interface ISnapshotCapture
{
    Task<BlockchainSnapshot> CaptureAsync(ChainId chain, CancellationToken cancellationToken);
}

public sealed class ProviderException(string message, bool timedOut = false, Exception? inner = null)
    : Exception(message, inner)
{
    public bool TimedOut { get; } = timedOut;
}

public sealed class RequestValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
