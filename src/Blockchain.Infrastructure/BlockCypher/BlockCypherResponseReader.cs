using System.Text;
using System.Text.Json;
using Blockchain.Application.Providers;

namespace Blockchain.Infrastructure.BlockCypher;

internal static class BlockCypherResponseReader
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, string expectedNetwork,
        int maxResponseBytes, CancellationToken cancellationToken)
    {
        var json = await ReadJsonAsync(response, maxResponseBytes, cancellationToken);
        ValidateNetwork(json, expectedNetwork);
        return json;
    }

    private static async Task<string> ReadJsonAsync(
        HttpResponseMessage response, int maxResponseBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > maxResponseBytes)
        {
            throw new ProviderException("Provider response is too large.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(bytes, cancellationToken)) != 0)
        {
            if (buffer.Length + bytesRead > maxResponseBytes)
            {
                throw new ProviderException("Provider response is too large.");
            }

            buffer.Write(bytes, 0, bytesRead);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ProviderException("Provider returned invalid UTF-8.", inner: ex);
        }
    }

    private static void ValidateNetwork(string payload, string expectedResponseName)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                !string.Equals(name.GetString(), expectedResponseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProviderException("Provider returned an unexpected network document.");
            }
        }
        catch (JsonException ex)
        {
            throw new ProviderException("Provider returned invalid JSON.", inner: ex);
        }
    }
}
