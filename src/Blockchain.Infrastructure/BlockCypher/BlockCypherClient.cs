using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Blockchain.Application.Providers;
using Blockchain.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.BlockCypher;

public sealed class BlockCypherClient(HttpClient http, ProviderRequestLimiter requestLimiter, ProviderCircuitBreaker circuitBreaker,
    IBlockCypherNetworkMappings mappings,
    IOptions<BlockCypherOptions> options, TimeProvider clock, ILogger<BlockCypherClient> logger)
    : IBlockchainDataClient
{
    private readonly ProviderResiliencePipeline resilience = new(options.Value, clock, requestLimiter, circuitBreaker);

    public async Task<ProviderSnapshot> FetchSnapshotAsync(BlockchainNetwork network, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds), clock);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var mapping = await mappings.FindAsync(network.Id, budget.Token)
                ?? throw new ProviderException("No BlockCypher source is configured for this network.");
            var response = await resilience.ExecuteAsync(token => SendAttemptAsync(mapping, settings, token), budget.Token);
            if ((int)response.StatusCode is < 200 or >= 300)
                throw new ProviderException($"Provider returned HTTP {(int)response.StatusCode}.");
            return new(response.Payload!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException("Provider request timed out.", timedOut: true);
        }
        catch (HttpRequestException ex) { throw new ProviderException("Provider could not be reached.", inner: ex); }
        catch (IOException ex) { throw new ProviderException("Provider response could not be read.", inner: ex); }
        catch (ProviderAttemptTimeoutException ex) { throw new ProviderException("Provider request timed out.", timedOut: true, inner: ex); }
        finally
        {
            logger.LogInformation("Provider {Network} finished in {ElapsedMs} ms", network,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async ValueTask<ProviderAttemptResult> SendAttemptAsync(BlockCypherNetworkMapping mapping,
        BlockCypherOptions settings, CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds), clock);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, mapping.RelativePath);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            logger.LogInformation("Provider {Source}: HTTP {StatusCode}", mapping.RelativePath, (int)response.StatusCode);
            if (!response.IsSuccessStatusCode) return new(response.StatusCode, null);
            var payload = await ReadPayloadAsync(response, settings.MaxResponseBytes, timeout.Token);
            ValidatePayload(payload, mapping.ExpectedResponseName);
            return new(response.StatusCode, payload);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new ProviderAttemptTimeoutException(ex);
        }
    }

    private static async Task<string> ReadPayloadAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new ProviderException("Provider response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(bytes, token)) != 0)
        {
            if (buffer.Length + read > limit) throw new ProviderException("Provider response is too large.");
            buffer.Write(bytes, 0, read);
        }
        try { return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length); }
        catch (DecoderFallbackException ex) { throw new ProviderException("Provider returned invalid UTF-8.", inner: ex); }
    }

    private static void ValidatePayload(string payload, string expectedResponseName)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                !string.Equals(name.GetString(), expectedResponseName, StringComparison.OrdinalIgnoreCase))
                throw new ProviderException("Provider returned an unexpected network document.");
        }
        catch (JsonException ex) { throw new ProviderException("Provider returned invalid JSON.", inner: ex); }
    }
}
