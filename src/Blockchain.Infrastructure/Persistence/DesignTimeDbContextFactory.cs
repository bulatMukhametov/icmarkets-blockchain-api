using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blockchain.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlockchainDbContext>
{
    public BlockchainDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Blockchain")
            ?? "Host=localhost;Database=blockchain;Username=blockchain;Password=local-development-only";
        return new(new DbContextOptionsBuilder<BlockchainDbContext>().UseNpgsql(connection).Options);
    }
}
