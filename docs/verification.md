# Verification record

Verified on 2026-10-18 in the Windows development workspace.

## Completed

- .NET SDK 10.0.402; dependencies restored with NuGet vulnerability warnings treated as errors.
- Release solution build: zero warnings and errors.
- Release tests: 28 passed, zero failed, zero skipped.
  - Unit: 7.
  - Integration: 11, including PostgreSQL migration/model consistency, complete payload preservation,
    currency/network isolation and keyset pagination with equal timestamps.
  - Functional: 10, including HTTP-to-PostgreSQL capture/history and concurrent partial collection.
- PostgreSQL tests used a temporary local PostgreSQL 17.11 instance through TEST_POSTGRES_CONNECTION.
  Each test fixture created and removed its own schema.
- Release publish succeeded with UseAppHost=false, as used by the Docker build.
- Published assembly applied migrations to a fresh database through --migrate.
- Published Kestrel host: liveness 200, readiness 200, five supported sources, Swagger JSON available.
- A real BTC mainnet capture through the published API returned 201. The stored snapshot was returned
  by GET history. The external provider was called only for this manual smoke test; automated tests
  use controlled responses.

## Prepared, not executed here

- Linux Docker image build and Docker Compose startup: Docker is not installed on this host.
- Testcontainers startup path: the same PostgreSQL tests ran against a local PostgreSQL connection;
  the container provisioning path is configured for CI.
- GitHub Actions: workflow prepared, execution requires publishing the repository.

Do not interpret this record as a successful Docker/CI run. See README for reproducible commands.
