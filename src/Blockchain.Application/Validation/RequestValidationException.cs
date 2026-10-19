namespace Blockchain.Application.Validation;

public sealed class RequestValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
