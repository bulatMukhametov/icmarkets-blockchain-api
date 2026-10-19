namespace Blockchain.Api.Contracts;

// Keep catalog entities and provider addressing out of the public HTTP contract.
public sealed record BlockchainNetworkResponse(string Coin, string Network);
