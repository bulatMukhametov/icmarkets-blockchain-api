namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed record NetworkCaptureResult(string Coin, string Network, long? SnapshotId, string? Error)
{
    public bool Succeeded => SnapshotId.HasValue;
}
