using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.BlockCypher;

public sealed class BlockCypherClient(HttpClient http, ProviderRequestLimiter gate, ProviderCircuitBreaker circuit,
    IBlockCypherNetworkMappings mappings,
    IOptions<BlockCypherOptions> options, TimeProvider clock, ILogger<BlockCypherClient> logger)
    : IBlockchainDataClient
{
    public async Task<ProviderSnapshot> FetchSnapshotAsync(BlockchainNetwork chain, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds), clock);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = Stopwatch.GetTimestamp();
        DateTimeOffset? requestedAt = null;
        try
        {
            var mapping = await mappings.FindAsync(chain.Id, budget.Token)
                ?? throw new ProviderException("No BlockCypher source is configured for this network.");
            for (var attempt = 0; ; attempt++)
            {
                circuit.ThrowIfUnavailable(budget.Token);
                await gate.WaitForPermitAsync(budget.Token);
                try
                {
                    var response = await circuit.ExecuteAsync(token =>
                    {
                        requestedAt ??= clock.GetUtcNow();
                        return SendAttemptAsync(mapping, attempt, settings, token);
                    }, budget.Token);
                    if (IsTransient(response.StatusCode) && attempt < settings.MaxRetries)
                    {
                        circuit.ThrowIfUnavailable(budget.Token);
                        var wait = response.RetryAfter ?? TimeSpan.FromMilliseconds(500 * (attempt + 1));
                        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                        // Long Retry-After values must not turn an HTTP request into a background job.
                        if (wait > TimeSpan.FromSeconds(5))
                            throw new ProviderException("Provider requested a delayed retry.");
                        await Task.Delay(wait, clock, budget.Token);
                        continue;
                    }
                    if ((int)response.StatusCode is < 200 or >= 300)
                        throw new ProviderException($"Provider returned HTTP {(int)response.StatusCode}.");
                    return new(response.Payload!, requestedAt!.Value);
                }
                catch (Exception ex) when (attempt < settings.MaxRetries &&
                    ex is HttpRequestException or IOException or ProviderAttemptTimeoutException)
                {
                    circuit.ThrowIfUnavailable(budget.Token);
                    await Task.Delay(TimeSpan.FromMilliseconds(500), clock, budget.Token);
                }
            }
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
            logger.LogInformation("Provider {Chain} finished in {ElapsedMs} ms", chain,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async ValueTask<ProviderAttemptResult> SendAttemptAsync(BlockCypherNetworkMapping mapping, int attempt,
        BlockCypherOptions settings, CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds), clock);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, mapping.RelativePath);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            logger.LogInformation("Provider {Source} attempt {Attempt}: HTTP {StatusCode}", mapping.RelativePath, attempt + 1, (int)response.StatusCode);
            var retryAfter = response.Headers.RetryAfter;
            var delay = retryAfter?.Delta ?? (retryAfter?.Date - clock.GetUtcNow());
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                delay ??= TimeSpan.FromSeconds(1);
                gate.ApplyCooldown(delay.Value);
            }
            if (!response.IsSuccessStatusCode) return new(response.StatusCode, null, delay);
            var payload = await ReadPayloadAsync(response, settings.MaxResponseBytes, timeout.Token);
            ValidatePayload(payload, mapping.ExpectedResponseName);
            return new(response.StatusCode, payload, delay);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new ProviderAttemptTimeoutException(ex);
        }
    }

    private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests
        or HttpStatusCode.RequestTimeout || (int)status >= 500;

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
                throw new ProviderException("Provider returned an unexpected chain document.");
        }
        catch (JsonException ex) { throw new ProviderException("Provider returned invalid JSON.", inner: ex); }
    }
}
