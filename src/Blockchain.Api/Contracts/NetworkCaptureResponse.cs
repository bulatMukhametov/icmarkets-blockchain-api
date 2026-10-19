using Blockchain.Application.Snapshots.CaptureAllSnapshots;

namespace Blockchain.Api.Contracts;

public sealed record NetworkCaptureResponse(string Coin, string Network, long? SnapshotId, string? Error)
{
    public bool Succeeded => SnapshotId.HasValue;

    public static NetworkCaptureResponse From(NetworkCaptureResult result) =>
        new(result.CurrencyCode, result.NetworkCode, result.SnapshotId, result.Failure switch
        {
            null => null,
            SnapshotCaptureFailure.ProviderUnavailable => "provider_unavailable",
            SnapshotCaptureFailure.ProviderTimeout => "provider_timeout",
            SnapshotCaptureFailure.ProviderCircuitOpen => "provider_circuit_open",
            _ => throw new ArgumentOutOfRangeException(nameof(result), "Unknown capture failure.")
        });
}
