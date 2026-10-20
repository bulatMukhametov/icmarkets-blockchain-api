# Blockchain snapshots API

ICMarkets .NET exercise: capture BlockCypher chain summaries, preserve their complete JSON responses,
and query the stored observation history. .NET 10, ASP.NET Core, EF Core and PostgreSQL.

## Assignment coverage

| Requirement | Implementation |
| --- | --- |
| Clean or vertical architecture with SOLID | Domain, Application, Infrastructure and API projects; use-case handlers depend on application interfaces |
| Store data from the five listed BlockCypher URLs | Initial catalog contains ETH main, DASH main, BTC main/test3 and LTC main; captures store complete response JSON |
| Swagger endpoints and stored history | Capture, batch capture, catalog and history endpoints; Swagger UI in Development and local Compose |
| Additional CreatedAt and descending history | UTC capture-start timestamp; history ordered by CreatedAt DESC, Id DESC; interpretation explained below |
| Health checks and CORS | Separate liveness/readiness endpoints and explicit allowed browser origins |
| DI, logging, mapping, serialization and validation | Constructor injection, structured logs, explicit response mapping, System.Text.Json and FluentValidation |
| Integration, Functional and Unit test projects | Three projects; PostgreSQL integration uses Testcontainers when database tests are enabled |
| Local .NET and Linux Docker execution | Launch profiles and a multi-stage Dockerfile with Compose database/migration/API services |
| .NET >= 6 and a chosen database | .NET 10 and PostgreSQL 17 through EF Core |
| Performance, inheritance and scalability considerations | Async I/O, indexed history, no-tracking reads and documented scaling limits; BackgroundService and AbstractValidator inheritance where useful, without artificial entity hierarchies |
| Asynchronous or parallel patterns | async/await and Task.WhenAll for collecting configured networks |
| At least two design patterns | CQRS and specialized Repository; EF Core DbContext provides the unit-of-work behavior |
| Optional API Gateway | Deliberately omitted for a single API deployment |
| Public GitHub repository, README, main/development branches | Repository link and final delivery checks are listed at the end; local changes still need to be committed and published before submission |

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
dotnet run --project src/Blockchain.Api --launch-profile http -- --migrate
dotnet run --project src/Blockchain.Api --launch-profile http
```

The Development settings contain a demo PostgreSQL connection: localhost:5432, database/user
`blockchain`, password `local-development-only`, matching local Compose. This is a public demo value,
not a deployment secret. Override it with User Secrets or `ConnectionStrings__Blockchain`.
The `https` launch profile
also listens on port 7080 and requires a trusted development certificate (`dotnet dev-certs https --trust`).

For this Windows workspace a local SDK is available under `.tools/dotnet` (ignored by Git).
Use `./scripts/dotnet.ps1` in place of `dotnet` if the system SDK is older than .NET 10, for example:

```powershell
./scripts/dotnet.ps1 test Blockchain.slnx
```

Fresh clones need their own .NET 10 SDK installation; the helper falls back to the system SDK.

The development profile enables Swagger. Outside Development enable it explicitly with
`Swagger__Enabled=true`. HTTPS termination for containers belongs at the deployment ingress.

### Configuration and secrets

- `appsettings.json`: shared non-secret settings (provider limits/timeouts, worker schedule, logging).
  No connection string is supplied here; CORS origins are empty by default.
- `appsettings.Development.json`: local demo database connection and localhost CORS origin.
- User Secrets: personal development credentials stored outside the repository. The API project
  includes a UserSecretsId; the default host loads them in Development. They are not encrypted
  and are not a production secret store.
- `launchSettings.json`: local HTTP/HTTPS launch profiles and the Development environment.
- `.env`: optional Docker Compose overrides; ignored by Git and Docker build context.
  Copy `.env.example` to start. .NET does not read this file directly.
- `compose.yaml`: a local demonstration deployment; supplies connection strings through environment
  variables for both migration and API containers, and explicitly enables local Swagger/CORS.
- Deployments: supply settings through environment variables and actual credentials through the
  hosting platform's secret store. The shared appsettings file provides no production credentials.

To override the local connection, replace the placeholder with your actual development connection:

```sh
dotnet user-secrets set "ConnectionStrings:Blockchain" "<your PostgreSQL connection string>" --project src/Blockchain.Api
```

Default precedence (later wins): appsettings.json, appsettings.{Environment}.json, Development User
Secrets, environment variables, command-line arguments. Environment keys use double underscores,
for example `ConnectionStrings__Blockchain` or `BlockCypher__MaxRetries`.
`appsettings.Local.json` is ignored as a precaution but is not loaded; use User Secrets instead.
The design-time EF factory requires `ConnectionStrings__Blockchain` when using `dotnet ef` and does
not read User Secrets. The `--migrate` application mode uses the normal host configuration above.
Provider and worker Options are still validated at startup.

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
CreatedAt is the UTC time captured in CaptureSnapshotHandler immediately before calling the provider client.
It includes local limiter waiting and retries; it is not the provider block time or database insertion time.
The brief describes the timestamp as the time requested from the API endpoint, without distinguishing
the incoming request from the outgoing provider call. This solution interprets it as the start of
fetching one network, after validation/catalog lookup. That definition also works for background collection.
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
| `ConnectionStrings:Blockchain` | Demo connection in Development only | Supply via User Secrets or environment for other connections |
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
| `Cors:AllowedOrigins` | Empty; localhost:5173 in Development/local Compose | Explicit browser origins |
| `Swagger:Enabled` | false | Enable Swagger outside Development |

The [provider documents](https://www.blockcypher.com/dev/#rate-limits-and-tokens) a free-tier ceiling
of 3 requests/sec and 100 requests/hour. All attempts consume the local quota, including retries.
Quota exhaustion fails promptly; external 429s fail without retries. The quota is shared
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
depend on the circuit. Polly retries network/I/O failures, attempt timeouts, HTTP 408 and 5xx with
a fixed 500 ms delay, up to MaxRetries. Every attempt passes through the shared limiter and breaker.
HTTP 429 and other non-transient 4xx return 502 / provider_unavailable without retries. Retry-After
from upstream is not used to pause other calls; a subsequent manual or scheduled capture may try again.
This intentionally simple policy keeps local rate/quota limiting but does not coordinate provider cooldowns.

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
| `src/Blockchain.Infrastructure/BlockCypher` | HTTP integration, Polly retries, request limiting and circuit breaker |
| `src/Blockchain.Infrastructure/Persistence` | EF Core mappings, initial catalog, migrations and repositories |
| `src/Blockchain.Infrastructure/Persistence/Configurations` | One EF Core mapping configuration per entity |
| `src/Blockchain.Infrastructure/Collection` | Optional periodic capture using the existing application command |

In the provider integration, `BlockCypherClient` handles HTTP calls and error translation,
`BlockCypherResponseReader` reads and validates the original JSON, and `ProviderResiliencePipeline`
coordinates retries, request limiting and the shared circuit breaker.
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

## Deliberate simplifications and limitations

These are scope decisions, not claims of production readiness or omitted mandatory functionality:

| Decision | Consequence and possible next step |
| --- | --- |
| Page-number pagination | Concurrent inserts can shift page boundaries and repeat rows; deep offsets cost more. Cursor pagination is the documented alternative for a larger history. |
| Task.WhenAll over the configured catalog | Appropriate for the small initial catalog; many networks would create many pending operations/scopes. Add bounded concurrency when catalog size justifies it. |
| In-process background worker | No durable schedule, catch-up, leader election or exactly-once guarantee. Enable it on one instance; independent scaling needs a worker host and coordination. |
| Process-local provider quota and circuit state | Limits reset on restart and are not shared between replicas or other clients using the same provider budget. Multiple instances need coordinated provider access. |
| Simple retry policy | Polly retries transient failures only. HTTP 429 fails without retry or shared Retry-After cooldown; future captures may try again under the local limits. |
| One implemented provider | New BlockCypher-supported networks can be configured in the database. Replacing BlockCypher requires an adapter, registration and changes to the catalog's collectable-network selection/mapping. |
| No catalog administration API | Catalog changes use controlled SQL as described in docs/catalog.md; no runtime currency enum limits expansion. |
| Independent snapshot commits | Partial batch results remain saved. There is no batch-wide transaction or POST idempotency; retrying a capture can append another observation. |
| One database, no cache or read replicas | Keeps the assignment straightforward. There are no measured throughput/latency guarantees: load and soak testing have not been performed. |
| No authentication or production operations setup | Compose binds to localhost. Public deployment needs authentication/authorization, secret management, metrics/alerts, retention and a tested backup/restore process. Readiness does not measure data freshness. |
| No API Gateway or Event Sourcing | A gateway is optional in the brief; CQRS and Repository already meet the pattern requirement. Neither additional component is needed for this scope. |

The three test projects verify behavior and failure handling, not production capacity. Automated provider
tests use controlled HTTP responses; local live-provider and Docker checks are dated in the verification
record. Later source changes do not imply the running Docker image has been rebuilt. GitHub Actions
execution is not claimed as verified.

The catalog migration is **forward-only**: its Down method rejects rollback because the old schema cannot
represent an extended catalog. Before upgrading an existing database, stop the old application and take
a backup. Rollback requires a verified pre-upgrade backup and the matching older application version.
See [catalog migration details](docs/catalog.md#existing-data-and-migration). Creating a backup alone is
not proof that restoration has been tested.

## Migrations and delivery

To add a migration:

```sh
dotnet tool restore
```

Set a development connection explicitly for the design-time factory (replace the placeholder):

```sh
# Bash
export ConnectionStrings__Blockchain='<your PostgreSQL connection string>'
```

```powershell
# PowerShell
$env:ConnectionStrings__Blockchain = '<your PostgreSQL connection string>'
```

Then run:

```sh
dotnet ef migrations add MeaningfulName --project src/Blockchain.Infrastructure --startup-project src/Blockchain.Infrastructure --output-dir Persistence/Migrations
```

Repository: [icmarkets-blockchain-api](https://github.com/bulatMukhametov/icmarkets-blockchain-api).
Before sending the submission:

1. Run the complete suite with database tests enabled and build/smoke-test the final Linux image.
2. Commit and push the final changes to development, and bring main to the reviewed submission version.
   Verify that both remote branches exist and that main contains the intended final code.
3. Verify public repository access and send its URL as requested in the brief. Email submission is manual.

The presence of a repository link or both branches does not imply local changes have already been
published. Consult the verification record for completed checks; CI configuration is not CI execution evidence.
