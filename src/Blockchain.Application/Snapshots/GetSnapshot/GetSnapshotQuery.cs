namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed record GetSnapshotQuery(string CurrencyCode, string NetworkCode, long Id);
