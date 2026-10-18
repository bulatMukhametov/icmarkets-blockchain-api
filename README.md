# Blockchain snapshots API

ICMarkets .NET exercise: capture BlockCypher chain summaries, preserve their complete JSON responses,
and query the stored observation history. .NET 10, ASP.NET Core, EF Core and PostgreSQL.

## Quick start: Linux containers

Requires Docker Engine/Desktop with Linux containers and Docker Compose v2.

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
| GET | `/api/chains` | Five supported currency/network pairs |
| POST | `/api/chains/{coin}/{network}/snapshots` | Capture and persist; 201 with Location |
| GET | `/api/chains/{coin}/{network}/snapshots/{id}` | Stored snapshot or 404 |
| GET | `/api/chains/{coin}/{network}/snapshots?pageSize=20&cursor=...` | Ordered history page |
| POST | `/api/snapshot-collections` | Completed report for all five sources; 200, possibly partial success |
| GET | `/health/live` | Process liveness |
| GET | `/health/ready` | Database/schema readiness |

Supported pairs: `eth/main`, `dash/main`, `btc/main`, `btc/test3`, `ltc/main`.
The visible LTC URL in the brief is used; its PDF hyperlink incorrectly points to BTC.
"All data" means the full summaries returned by the five listed URLs, rather than downloading blocks
or transactions linked from those documents. GET history never triggers collection.

A snapshot response contains `id`, `coin`, `network`, `createdAt` and `payload` (a JSON object).
CreatedAt is UTC request-start time, not the provider's `time` field or insertion-completion time.
Every successful POST appends an observation, even if the blockchain did not change.
History is ordered by CreatedAt DESC, Id DESC; pageSize is 1..100. Follow `nextCursor` until null.
Encode the returned cursor as a URL query parameter. Do not interpret its contents as a stable API contract.

Invalid parameters/cursors return 400 with ValidationProblemDetails; missing snapshots return 404.
Provider errors return 502, provider timeouts 504, incoming capture saturation 429, unexpected storage
failures 500. Error responses contain a trace ID and omit internal exceptions. A batch reports provider
errors per chain; a storage failure returns 500 and does not roll back already committed captures.

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
| `Cors:AllowedOrigins` | `http://localhost:5173` | Explicit browser origins |
| `Swagger:Enabled` | false | Enable Swagger outside Development |

The [provider documents](https://www.blockcypher.com/dev/#rate-limits-and-tokens) a free-tier ceiling
of 3 requests/sec and 100 requests/hour. All attempts consume the local quota, including retries.
Quota exhaustion fails promptly; external 429s are handled through bounded retries. The quota is shared
inside one process only, resets on restart and does not coordinate other applications using the same IP.

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
functional behavior respectively. CI enables PostgreSQL tests and builds the Linux image.

## Architecture and maintenance

- Clean Architecture: Domain ← Application ← Infrastructure; API is the composition root.
- CQRS: separate command/query handlers, one database, no mediator dependency.
- Specialized Repository: append, find and keyset history; no generic CRUD or IQueryable leakage.
- EF Core DbContext provides Unit of Work; one append is one transaction.
- Batch calls use independent DI scopes/DbContexts and preserve successful observations.

See [architecture decisions](docs/architecture.md) for tradeoffs, failure semantics and scaling limits.
See [verification record](docs/verification.md) for completed checks and environment limitations,
and [sample requests](requests.http) for an executable HTTP walkthrough.
Event Sourcing, a gateway and a scheduler are intentionally outside the current requirements.

To add a migration:

```sh
dotnet tool restore
dotnet ef migrations add MeaningfulName --project src/Blockchain.Infrastructure --startup-project src/Blockchain.Infrastructure --output-dir Persistence/Migrations
```

The submission uses `main` and `development`. Publishing the public repository and sending its URL
are separate submission steps; this project does not send email automatically.
