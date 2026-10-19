namespace Blockchain.Infrastructure.BlockCypher;

internal sealed class ProviderAttemptTimeoutException(Exception inner) : TimeoutException("Provider attempt timed out.", inner);
