using System.Text.Json;
using Blockchain.Domain;

namespace Blockchain.Application.Abstractions;

public sealed record SnapshotCursor(string Coin, string Network, DateTimeOffset CreatedAt, long Id)
{
    public static string Encode(BlockchainNetworkSnapshot snapshot) => Convert.ToBase64String(
        JsonSerializer.SerializeToUtf8Bytes(new SnapshotCursor(
            snapshot.BlockchainNetwork.Cryptocurrency.Code, snapshot.BlockchainNetwork.Code, snapshot.RequestedAtUtc, snapshot.Id)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static SnapshotCursor? Decode(string? value, BlockchainNetwork chain)
    {
        if (value is null) return null;
        try
        {
            if (value.Length is 0 or > 512) throw new FormatException();
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
            var cursor = JsonSerializer.Deserialize<SnapshotCursor>(Convert.FromBase64String(padded));
            if (cursor is null || cursor.Coin != chain.Cryptocurrency.Code || cursor.Network != chain.Code ||
                cursor.Id <= 0 || cursor.CreatedAt == default || cursor.CreatedAt.Offset != TimeSpan.Zero)
                throw new FormatException();
            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new RequestValidationException("cursor", "Invalid cursor for the requested chain.");
        }
    }
}
