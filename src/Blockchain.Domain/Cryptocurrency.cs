namespace Blockchain.Domain;

/// <summary>A currency in the application catalog. Its code is a stable public identifier.</summary>
public sealed class Cryptocurrency
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private Cryptocurrency() { }

    public Cryptocurrency(Guid id, string code, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Currency ID must not be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 100) throw new ArgumentException("Currency name must fit 100 characters.", nameof(name));
        Id = id;
        Code = CatalogCode.Normalize(code);
        Name = name.Trim();
    }
}
