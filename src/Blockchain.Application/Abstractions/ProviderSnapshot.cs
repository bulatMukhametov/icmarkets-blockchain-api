namespace Blockchain.Application.Abstractions;

public sealed record ProviderSnapshot(string Payload, DateTimeOffset RequestedAt);
