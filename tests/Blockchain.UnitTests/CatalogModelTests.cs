using Blockchain.Domain;

namespace Blockchain.UnitTests;

public sealed class CatalogModelTests
{
    [Theory]
    [InlineData("")]
    [InlineData("../btc")]
    [InlineData("bitcoin main")]
    [InlineData("бтс")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
    public void Invalid_codes_are_rejected_for_both_currency_and_network(string code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Cryptocurrency(Guid.NewGuid(), code, "Bitcoin"));
        var currency = new Cryptocurrency(Guid.NewGuid(), "btc", "Bitcoin");
        Assert.ThrowsAny<ArgumentException>(() => new BlockchainNetwork(Guid.NewGuid(), currency, code, NetworkKind.Mainnet));
    }

    [Fact]
    public void Empty_identity_or_undefined_network_kind_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Cryptocurrency(Guid.Empty, "btc", "Bitcoin"));
        var currency = new Cryptocurrency(Guid.NewGuid(), "btc", "Bitcoin");
        Assert.Throws<ArgumentException>(() => new BlockchainNetwork(Guid.Empty, currency, "main", NetworkKind.Mainnet));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockchainNetwork(Guid.NewGuid(), currency, "main", (NetworkKind)42));
    }
}
