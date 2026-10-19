namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public enum SnapshotCaptureFailure
{
    ProviderUnavailable,
    ProviderTimeout,
    ProviderCircuitOpen
}
