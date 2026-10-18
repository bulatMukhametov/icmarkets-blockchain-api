namespace Blockchain.Tests.Shared;

public sealed class DatabaseFactAttribute : Xunit.FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Set RUN_DATABASE_TESTS=true to run PostgreSQL tests (Docker or TEST_POSTGRES_CONNECTION required).";
    }
}
