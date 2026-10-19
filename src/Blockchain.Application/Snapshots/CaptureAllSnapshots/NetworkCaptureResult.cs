namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed record NetworkCaptureResult(string CurrencyCode, string NetworkCode, long? SnapshotId, SnapshotCaptureFailure? Failure)
{
    public bool Succeeded => SnapshotId.HasValue;
}
