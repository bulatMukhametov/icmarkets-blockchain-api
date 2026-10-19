# Blockchain snapshots API

ICMarkets .NET exercise: capture BlockCypher chain summaries, preserve their complete JSON responses,
and query the stored observation history. .NET 10, ASP.NET Core, EF Core and PostgreSQL.

## What the application stores

A **blockchain network** is a currency/network pair: `btc/main` is Bitcoin mainnet, while `btc/test3`
is a separate Bitcoin test network. A **snapshot** is one saved response from the provider about that
network, together with the time our request started. It is a network summary, not a copy of all blocks.
Two requests produce two observations even when the returned data is identical.

The catalog contains `Cryptocurrency` entities and their `BlockchainNetwork` entities. `NetworkKind`
classifies a network as Mainnet or Testnet; its code identifies the specific network. A
`BlockchainNetworkSnapshot` references the network by ID. `CreatedAt` records request-start time
and `RawResponseJson` preserves the original JSON. The HTTP names remain `createdAt` and `payload`.
See [catalog model and adding a currency](docs/catalog.md) for the relationships and an SQL example.

## Quick start: Linux containers

Requires Docker Engine/Desktop with Linux containers and Docker Compose v2 or later.

```sh
docker compose up --build -d
docker compose logs migrate api
```

Compose starts PostgreSQL, applies migrations in a one-off job, then starts the API.
Open [Swagger](http://localhost:5080/swagger), [liveness](http://localhost:5080/health/live)
or [readiness](http://localhost:5080/health/ready). API and database ports bind to localhost.

```sh
curl -X POST http://localhost:5080/api/chains/btc/main/snapshots
curl 'http://localhost:5080/api/chains/btc/main/snapshots?pageSize=20'
curl -X POST http://localhost:5080/api/snapshot-collections
docker compose down
```

The database volume survives `down`. Set POSTGRES_PASSWORD in a local `.env` when overriding the demo
password; `.env.example` documents the default. Changing it on an existing volume does not change the
database role password. No provider token is needed for these read-only upstream calls.

The local Compose database uses password authentication without Kerberos. Its connection strings set
`GSS Encryption Mode=Disable` to avoid Npgsql 10 probing for a missing Kerberos library in the runtime
image, as described in the [Npgsql release notes](https://www.npgsql.org/doc/release-notes/10.0.html#cannot-load-library-libgssapi_krb5so2-when-kerberos-is-not-installed).

## Local .NET profile

Requires .NET 10 SDK and PostgreSQL 17 (or start only the database with `docker compose up postgres -d`).

```sh
dotnet restore Blockchain.slnx
dotnet build Blockchain.slnx
dotnet run --project src/Blockchain.Api -- --migrate
dotnet run --project src/Blockchain.Api --launch-profile http
```

Default PostgreSQL connection: localhost:5432, database/user `blockchain`, password
`local-development-only`. Override using `ConnectionStrings__Blockchain`. The `https` launch profile
also listens on port 7080 and requires a trusted development certificate (`dotnet dev-certs https --trust`).

For this Windows workspace a local SDK is available under `.tools/dotnet` (ignored by Git).
Use `./scripts/dotnet.ps1` in place of `dotnet` if the system SDK is older than .NET 10, for example:

```powershell
./scripts/dotnet.ps1 test Blockchain.slnx
```

Fresh clones need their own .NET 10 SDK installation; the helper falls back to the system SDK.

The development profile enables Swagger. Outside Development enable it explicitly with
`Swagger__Enabled=true`. HTTPS termination for containers belongs at the deployment ingress.

## HTTP contract

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/chains` | Currency/network pairs from the database catalog |
| POST | `/api/chains/{coin}/{network}/snapshots` | Capture and persist; 201 with Location |
| GET | `/api/chains/{coin}/{network}/snapshots/{id}` | Stored snapshot or 404 |
| GET | `/api/chains/{coin}/{network}/snapshots?page=1&pageSize=20` | Ordered history page |
| POST | `/api/snapshot-collections` | Completed report for all configured sources; 200, possibly partial success |
| GET | `/health/live` | Process liveness |
| GET | `/health/ready` | Database/schema readiness |

Initially configured pairs: `eth/main`, `dash/main`, `btc/main`, `btc/test3`, `ltc/main`.
The catalog is extensible without changing handlers or the database schema. A provider mapping is
required to collect a catalog network; history remains readable after its mapping is removed.
The visible LTC URL in the brief is used; its PDF hyperlink incorrectly points to BTC.
"All data" means the full summaries returned by the five listed URLs, rather than downloading blocks
or transactions linked from those documents. GET history never triggers collection.

A snapshot response contains `id`, `coin`, `network`, `createdAt` and `payload` (a JSON object).
CreatedAt is UTC request-start time, not the provider's `time` field or insertion-completion time.
Every successful POST appends an observation, even if the blockchain did not change.
History uses simple page-number pagination, ordered by CreatedAt DESC, Id DESC.
`page` starts at 1 (default 1), and `pageSize` is 1..100 (default 20).
The response contains `items`, `page`, `pageSize` and `hasNextPage`. Increment `page` while
`hasNextPage` is true. A page beyond the available history returns an empty list and `hasNextPage: false`.
The query uses `Skip((page - 1) * pageSize)` and reads one extra row to determine whether another
page exists, without a separate total-count query. Offsets greater than Int32.MaxValue return 400.

**Pagination trade-off:** each request reads the current database state. If a new snapshot is inserted
at the beginning between requests, existing rows shift: page 1 might return A/B/C, then after insertion
of X, page 2 returns C/D/E. Sorting by creation time does not freeze the dataset, so C is repeated.
Deep pages also become more expensive because PostgreSQL must skip the preceding rows.
This implementation favors simplicity for the assignment. For a large, frequently updated history,
cursor (keyset) pagination can instead pass the last returned `(CreatedAt, Id)` and request rows where
`CreatedAt < lastCreatedAt OR (CreatedAt = lastCreatedAt AND Id < lastId)`, filtered by network.
The existing network/time/ID index supports that approach. It avoids shifts caused by newer rows at
the beginning and large offsets, but does not support jumping directly to an arbitrary page number
or provide a transactionally frozen export. Cursor pagination is a possible extension, not implemented here.

Invalid parameters return 400 with ValidationProblemDetails; missing snapshots return 404.
FluentValidation validators live beside application commands/queries and are called explicitly by
handlers. They return errors for all invalid fields together. ASP.NET Core handles parameter binding;
domain invariants and database constraints remain separate from request validation.
Provider errors return 502, provider timeouts 504, an open circuit returns 503 with Retry-After,
incoming capture saturation returns 429, and unexpected storage failures return 500. Error responses
contain a trace ID and omit internal exceptions. A batch reports provider errors per chain (including
`provider_circuit_open`); a storage failure returns 500 and does not roll back already committed captures.

## Configuration

ASP.NET configuration supports environment variables with `__` separators.

| Key | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings:Blockchain` | Local development connection | PostgreSQL |
| `BlockCypher:BaseUrl` | `https://api.blockcypher.com/v1/` | HTTPS provider URL; trailing slash required |
| `BlockCypher:RequestsPerSecond` | 2 | Spaced attempts; validated range 1..3 |
| `BlockCypher:RequestsPerHour` | 90 | Rolling per-process attempt budget; range 1..100 |
| `BlockCypher:AttemptTimeoutSeconds` | 10 | Per-attempt limit |
| `BlockCypher:TotalTimeoutSeconds` | 45 | Provider operation budget including waits/retries |
| `BlockCypher:MaxRetries` | 1 | Additional attempts; range 0..2 |
| `BlockCypher:MaxResponseBytes` | 1048576 | Maximum accepted response body |
| `BlockCypher:CircuitBreakerFailureRatio` | 0.5 | Availability failure ratio that opens the circuit; range 0.01..1 |
| `BlockCypher:CircuitBreakerMinimumThroughput` | 5 | Minimum attempts in the sampling window; range 2..100 |
| `BlockCypher:CircuitBreakerSamplingSeconds` | 30 | Sampling window; range 1..300 seconds |
| `BlockCypher:CircuitBreakerBreakSeconds` | 30 | Open duration before a recovery probe; range 1..300 seconds |
| `SnapshotCollection:Enabled` | false | Enable automatic collection of configured catalog networks in this API process |
| `SnapshotCollection:IntervalSeconds` | 600 | Delay after a cycle completes; range 1..86400 seconds |
| `SnapshotCollection:InitialDelaySeconds` | 10 | Delay before the first cycle; range 0..3600 seconds |
| `SnapshotCollection:CycleTimeoutSeconds` | 120 | Cooperative timeout for the entire cycle, including persistence; range 1..600 seconds |
| `Cors:AllowedOrigins` | `http://localhost:5173` | Explicit browser origins |
| `Swagger:Enabled` | false | Enable Swagger outside Development |

The [provider documents](https://www.blockcypher.com/dev/#rate-limits-and-tokens) a free-tier ceiling
of 3 requests/sec and 100 requests/hour. All attempts consume the local quota, including retries.
Quota exhaustion fails promptly; external 429s are handled through bounded retries. The quota is shared
inside one process only, resets on restart and does not coordinate other applications using the same IP.

A shared Polly circuit breaker monitors individual provider attempts, including retries. With the
defaults, at least five attempts in 30 seconds and a failure ratio of at least 50% open the circuit for
30 seconds. It then admits one recovery probe. Network/body-read failures, per-attempt timeouts, HTTP
408 and 5xx count as availability failures. HTTP 429, other 4xx, payload validation, local quota errors
and caller cancellation during normal operation do not trip the circuit. A cancelled recovery probe
reopens it, because cancellation does not establish that the provider recovered.

New calls fail promptly while the circuit is open; retries stop and requests waiting at the outbound
gate recheck circuit admission before sending. Calls already sent may finish. The circuit is shared
by all configured networks in one process and resets on restart. GET history and database readiness do not
depend on the circuit. HTTP 429 applies a shared Retry-After cooldown to the provider limiter, including
on the final attempt. Both delta-seconds and HTTP-date headers are supported; a missing header uses
one second. New or still-waiting calls fail promptly during this pause without consuming quota, while
already admitted requests may finish. A later response can extend but never shorten the pause.
The original call may retry after a short delay (up to five seconds); a longer delay fails promptly
while the shared pause remains active. Cooldown failures keep the existing 502 / provider_unavailable
contract and do not trip the availability circuit.

## Background collection

An optional BackgroundService invokes the same CaptureAllSnapshots command as the collection endpoint.
Each cycle reads configured networks from the database and starts their captures with Task.WhenAll. The shared outbound limiter still controls HTTP request rate and quota. There is no separate per-batch concurrency cap; a much larger catalog would need bounded concurrency.
Enable it locally with `SnapshotCollection__Enabled=true`. For Compose, set
`SNAPSHOT_COLLECTION_ENABLED=true` in `.env` and run `docker compose up -d`.
`SNAPSHOT_COLLECTION_INTERVAL_SECONDS` overrides the Compose interval (default 600).
Configuration changes require restarting/recreating the API process.

The first cycle starts after 10 seconds. Each cycle gets a fresh async DI scope; each parallel chain
capture still owns its own DbContext. The next delay starts after completion, so automatic cycles never
overlap and missed runs do not accumulate. Shutdown cancels active work. A 120-second cooperative cycle
timeout covers fetching and saving; successful commits survive partial failures or cancellation.
Provider failures are logged per chain; database errors and timeouts are logged at cycle level. The
worker continues after the configured delay. Migration-only execution (`--migrate`) never starts it.

Manual captures can run concurrently with automatic capture; both share the same in-process outbound
quota and circuit breaker. The worker does not consume the HTTP endpoint's concurrency permits. At
600 seconds with the five initial networks, successful automatic cycles consume about 30 provider requests/hour before retries;
manual calls share the remaining budget. Configuring one second does not override provider limits or
guarantee one-second freshness. Every successful collection appends a snapshot, including unchanged data.

Enable collection on only one instance. There is no distributed lock, durable schedule, catch-up after
downtime or exactly-once guarantee. Other API instances must disable it; their manual captures still need
shared quota coordination before using the same provider budget. For independently scaled ingestion,
move the hosted service to a dedicated worker host and coordinate all provider access. Readiness still
measures database access, not freshness; monitor collection logs and snapshot age separately.

## Tests

```sh
dotnet test Blockchain.slnx
```

This runs unit tests, provider-client tests with controlled HTTP responses and HTTP contract tests
using WebApplicationFactory and a test-only in-memory repository. PostgreSQL tests are visibly skipped
unless enabled; the test double is not used to verify EF or SQL behavior.

Run the complete suite with Docker available:

```sh
# Bash
RUN_DATABASE_TESTS=true dotnet test Blockchain.slnx
```

```powershell
# PowerShell
$env:RUN_DATABASE_TESTS = 'true'
dotnet test Blockchain.slnx
```

Testcontainers starts PostgreSQL 17 and applies the real migrations. Alternatively, set
`TEST_POSTGRES_CONNECTION` to a dedicated test database connection (CREATE SCHEMA permission required);
tests create unique schemas and drop only those schemas on completion. Still set RUN_DATABASE_TESTS=true.
Do not point this at a production database.

The three projects cover domain/application behavior, real persistence/provider integration, and API
functional behavior respectively. The included CI workflow is configured to run PostgreSQL tests and
build the Linux image; verification reported here is local, not a confirmed GitHub Actions result.

## Architecture and maintenance

Start reading the code in this order:

| Location | Responsibility |
| --- | --- |
| `src/Blockchain.Domain` | Currency and network entities, network kind, saved network snapshots |
| `src/Blockchain.Application/Snapshots` | Capture one/all networks and read stored history; command/query/result files sit beside their handler |
| `src/Blockchain.Application/Abstractions` | Command/query handler interfaces |
| `src/Blockchain.Application/Providers`, `Networks`, `Snapshots`, `Validation` | Provider and catalog ports, snapshot persistence/capture contracts, validation errors |
| `src/Blockchain.Infrastructure/BlockCypher` | HTTP integration, request limiting, shared cooldown and circuit breaker |
| `src/Blockchain.Infrastructure/Persistence` | EF Core mappings, initial catalog, migrations and repositories |
| `src/Blockchain.Infrastructure/Collection` | Optional periodic capture using the existing application command |
| `src/Blockchain.Api` | HTTP endpoints, response DTOs, errors, health and dependency registration |
| `src/Blockchain.Api/Configuration` | AddApplication and AddApi registrations; middleware order remains in Program.cs |

For a single capture, follow `SnapshotsController.CaptureSnapshot` -> `CaptureSnapshotHandler` ->
`IBlockchainDataClient.FetchSnapshotAsync` -> `IBlockchainSnapshotRepository.SaveAsync`.
For history, follow `GetSnapshotHistory` -> `GetSnapshotHistoryHandler` -> the repository; this path
never calls the provider. `HandleAsync` is the common command/query handler convention.
`ScopedSnapshotCaptureService` creates a separate DI scope for each concurrent capture so it owns
its own DbContext. `ProviderRequestLimiter.WaitForPermitAsync` enforces outbound request limits.

- Clean Architecture: Domain ← Application ← Infrastructure; API is the composition root.
- CQRS: separate command/query handlers, one database, no mediator dependency.
- Specialized Repository: append, find and paginated history; no generic CRUD or IQueryable leakage.
- EF Core DbContext provides Unit of Work; one append is one transaction.
- Batch calls use independent DI scopes/DbContexts and preserve successful observations.

See [architecture decisions](docs/architecture.md) for tradeoffs, failure semantics and scaling limits.
See [verification record](docs/verification.md) for completed checks and environment limitations,
and [sample requests](requests.http) for an executable HTTP walkthrough.
Event Sourcing, a gateway and a durable distributed scheduler are outside the current implementation.

To add a migration:

```sh
dotnet tool restore
dotnet ef migrations add MeaningfulName --project src/Blockchain.Infrastructure --startup-project src/Blockchain.Infrastructure --output-dir Persistence/Migrations
```

The submission uses `main` and `development`. Publishing the public repository and sending its URL
are separate submission steps; this project does not send email automatically.
