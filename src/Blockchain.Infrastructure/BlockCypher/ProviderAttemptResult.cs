using System.Net;

namespace Blockchain.Infrastructure.BlockCypher;

internal readonly record struct ProviderAttemptResult(HttpStatusCode StatusCode, string? Payload);
