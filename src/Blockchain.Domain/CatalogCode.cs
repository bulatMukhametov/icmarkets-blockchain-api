namespace Blockchain.Domain;

// Codes are stable route identifiers; they are not provider URLs or display names.
internal static class CatalogCode
{
    internal static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 32 || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new ArgumentException("Catalog codes must contain 1..32 ASCII letters, digits or hyphens.", nameof(value));
        return normalized;
    }
}
