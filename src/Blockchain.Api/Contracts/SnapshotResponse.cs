using System.Text.Json;
using Blockchain.Domain;

namespace Blockchain.Api.Contracts;

public sealed record SnapshotResponse(long Id, string Coin, string Network, DateTimeOffset CreatedAt, JsonElement Payload)
{
    public static SnapshotResponse From(BlockchainNetworkSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.RawResponseJson);
        return new(snapshot.Id, snapshot.BlockchainNetwork.Cryptocurrency.Code, snapshot.BlockchainNetwork.Code,
            snapshot.CreatedAt, document.RootElement.Clone());
    }
}
