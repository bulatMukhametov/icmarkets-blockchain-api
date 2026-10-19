namespace Blockchain.Application.Snapshots.CaptureSnapshot;

public sealed record CaptureSnapshotCommand(string CurrencyCode, string NetworkCode);
