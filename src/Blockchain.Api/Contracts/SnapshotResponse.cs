using System.Text.Json;
using Blockchain.Domain;

namespace Blockchain.Api.Contracts;

public sealed record SnapshotResponse(long Id, string Coin, string Network, DateTimeOffset CreatedAt, JsonElement Payload)
{
    public static SnapshotResponse From(BlockchainSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Payload);
        return new(snapshot.Id, snapshot.Coin, snapshot.Network, snapshot.CreatedAt, document.RootElement.Clone());
    }
}

public sealed record SnapshotHistoryResponse(IReadOnlyList<SnapshotResponse> Items, string? NextCursor);
