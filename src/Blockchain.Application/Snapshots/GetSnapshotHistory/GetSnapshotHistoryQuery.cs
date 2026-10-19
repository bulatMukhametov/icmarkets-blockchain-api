namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed record GetSnapshotHistoryQuery(string CurrencyCode, string NetworkCode, int Page = 1, int PageSize = 20);
