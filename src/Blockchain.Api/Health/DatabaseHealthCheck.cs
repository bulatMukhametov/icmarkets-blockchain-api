using Blockchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blockchain.Api.Health;

public sealed class DatabaseHealthCheck(BlockchainDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        // Also requires the schema, not just a reachable database server.
        _ = await db.Snapshots.AsNoTracking().Select(x => x.BlockchainNetworkId).Take(1).ToArrayAsync(cancellationToken);
        _ = await db.Networks.AsNoTracking().Select(x => x.Cryptocurrency.Code).Take(1).ToArrayAsync(cancellationToken);
        _ = await db.BlockCypherMappings.AsNoTracking().AnyAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
