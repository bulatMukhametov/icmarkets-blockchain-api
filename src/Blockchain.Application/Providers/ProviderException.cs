namespace Blockchain.Application.Providers;

public class ProviderException(string message, bool timedOut = false, Exception? inner = null)
    : Exception(message, inner)
{
    public bool TimedOut { get; } = timedOut;
}
