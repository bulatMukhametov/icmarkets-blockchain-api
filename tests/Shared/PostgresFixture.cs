using Blockchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Blockchain.Tests.Shared;

public sealed class PostgresFixture : Xunit.IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? schema;
    public string ConnectionString { get; private set; } = null!;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task StartAsync()
    {
        if (ConnectionString is not null) return;
        var connection = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await container.StartAsync();
            connection = container.GetConnectionString();
        }
        schema = "test_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
        ConnectionString = settings.ConnectionString;
        await using var dbConnection = new NpgsqlConnection(ConnectionString);
        await dbConnection.OpenAsync();
        await using var command = dbConnection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{schema}\"";
        await command.ExecuteNonQueryAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public BlockchainDbContext CreateContext() => new(new DbContextOptionsBuilder<BlockchainDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
        else if (schema is not null)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA \"{schema}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }
}
