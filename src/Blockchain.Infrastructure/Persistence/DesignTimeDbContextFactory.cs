using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blockchain.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlockchainDbContext>
{
    public BlockchainDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Blockchain");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__Blockchain before running dotnet ef. The design-time factory does not load API User Secrets.");
        return new(new DbContextOptionsBuilder<BlockchainDbContext>().UseNpgsql(connection).Options);
    }
}
