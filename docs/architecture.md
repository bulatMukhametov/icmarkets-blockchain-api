# Architecture decisions

## 1. Clean Architecture and use-case folders

Domain has no framework dependencies. Application references Domain and FluentValidation and owns its ports.
Infrastructure implements persistence and the provider client. API owns HTTP contracts and the composition root.
There is one deployment unit and one database. Application folders group code by use case.

API controllers separate the network catalog (NetworksController), individual snapshots
(SnapshotsController) and batch capture (SnapshotCollectionsController). Existing routes remain
/api/chains, /api/chains/{coin}/{network}/snapshots and /api/snapshot-collections.
Program.cs calls AddApplication, AddInfrastructure and AddApi, then shows the middleware order
explicitly. AddApplication and AddApi live in the API Configuration folder as composition-root
helpers; Application itself does not depend on dependency-injection packages.
ApiExceptionHandler selects the response through small problem-creation methods and shares one
response-writing path, including the fallback for unsupported Accept headers.

The catalog has two entities with stable GUID identities: Cryptocurrency and BlockchainNetwork.
A currency code is unique; a network code is unique within its currency. NetworkKind is an enum for
Mainnet/Testnet classification, not an identifier for a particular network. BlockchainNetworkSnapshot
is a separate entity with a database-generated ID and a required network foreign key. No currency enum
or static runtime list limits expansion. Snapshot means an observation, not a complete blockchain
download. Domain properties CreatedAt and RawResponseJson map to the existing CreatedAt/Payload
database columns and HTTP fields. Public application contracts and use-case messages have named files.
Provider-specific addresses live in BlockCypherNetworkMappings, not in Domain. Catalog records without
a mapping remain readable but are excluded from automatic/batch collection; individual capture fails
as provider_unavailable until a mapping is supplied. See [catalog details](catalog.md).
No artificial base entities, aggregates or per-currency service inheritance are needed.

## 2. CQRS and Repository

Commands capture new observations. Queries only read stored observations. Handlers implement distinct
command/query interfaces; CQRS does not require MediatR, separate databases or messaging.

Public snapshot commands and queries accept currency/network codes. Each handler resolves the network
through the catalog, so calling a use case does not require the controller to load a domain entity first.
Batch capture uses the same command inside a separate scope per network, including its catalog lookup.
This deliberately keeps one capture path; the extra indexed lookup is small for this workload.
Application reports typed capture failures; API maps them to the existing public error strings.
Provider contracts, network catalog contracts and validation errors live in their respective folders.
History uses page/pageSize inputs and returns items, page, pageSize and hasNextPage.
CreatedAt is captured by CaptureSnapshotHandler before calling the provider client, using TimeProvider.

The specialized repository exposes append, find and ordered history operations, without IQueryable
or a generic CRUD abstraction. EF Core DbContext is the Unit of Work; each append calls SaveChangesAsync.
There is no additional IUnitOfWork wrapper because no scenario coordinates multiple repositories.

Persistence/Configurations contains one EF Core configuration per mapped entity. BlockchainDbContext
applies these configurations and the initial catalog; schema details stay beside persistence code.
AddInfrastructure groups dependency registration into persistence, BlockCypher and snapshot collection.

## 3. Original payload, timestamps and pagination

Store the full successful response as PostgreSQL text, with metadata in separate columns.
This preserves unknown fields, large numeric literals and original formatting. PostgreSQL constraints
require an object document and an existing catalog network. Referenced networks/currencies cannot be
deleted. HTTP responses wrap the payload as a JSON object.

CreatedAt is the UTC time immediately before Application calls the provider client, before limiter waiting.
It remains unchanged if a retry succeeds. Domain normalizes it to PostgreSQL's microsecond
precision. Each successful capture creates a new row, even if the payload did not change.

History uses offset pagination ordered by CreatedAt DESC, Id DESC. Id resolves equal timestamps.
The repository filters by network, skips (page - 1) * pageSize rows and reads at most pageSize + 1
observations without tracking. The extra row determines hasNextPage; no total-count query is needed.
Application validates positive page numbers, pageSize 1..100 and the Int32 offset bound before querying.
The index begins with BlockchainNetworkId and matches the time/ID ordering. Concurrent inserts can
shift page boundaries and cause repeated rows, and deep offsets cost more. README documents this
deliberate simplicity trade-off and a possible future cursor-based alternative.

## 4. Provider resilience and concurrency

BlockCypherClient resolves provider mapping, executes a resilient request and checks the response.
BlockCypherResponseReader reads the bounded response body, validates UTF-8, JSON and the expected
network name, then returns the original JSON unchanged.
ProviderResiliencePipeline configures Polly retries around the shared limiter and circuit breaker;
the client no longer owns a retry loop or timestamps. ProviderSnapshot contains only raw JSON.
The limiter tracks spacing and hourly quota. ScopedSnapshotCaptureService gives each capture its own scope.

IHttpClientFactory manages connections. A singleton request gate spaces attempts and enforces a rolling
hourly budget across all currencies and retries in this process. Defaults leave headroom below the
documented free-tier ceiling. The gate does not wait for an hourly reset: exhausted budgets fail promptly.

HTTP attempts have a timeout and the complete capture has a bounded provider time budget, including gate
waiting and retry delays. Polly retries network/I/O failures, attempt timeouts, 408 and 5xx with a fixed
500 ms delay. HTTP 429 and other 4xx are not retried; upstream Retry-After is not applied. Non-successful, invalid, oversized or wrong-chain
responses never become snapshots. Redirects are disabled. User-provided source URLs are not accepted.

A singleton Polly circuit breaker is shared across typed clients and all chains for the configured
provider. It samples individual HTTP attempts, including retries: by default at least five attempts
in 30 seconds with an availability failure ratio of 50% open it for 30 seconds. Network errors,
body-read I/O errors, per-attempt timeouts, 408 and 5xx count as failures. HTTP 429 and other 4xx,
payload validation, local gate failures and normal caller cancellation do not trip it. A cancelled
half-open probe reopens the circuit instead of claiming recovery or keeping the probe occupied.

Execution order is: total budget -> Polly retry policy -> fast circuit check -> request gate -> authoritative
circuit admission -> attempt timeout -> send/read/validate. Checking admission after the gate prevents
waiting requests from sending after another attempt opens the circuit. Gate reservations made during
an opening race may conservatively remain charged to the hourly budget. Already sent attempts can finish.
An open circuit is never retried locally. After the break, Polly admits one probe to establish availability.
Transitions are logged. Polly types remain in Infrastructure; Application exposes a provider-circuit
exception, mapped to HTTP 503 with Retry-After or a per-chain provider_circuit_open batch outcome.

This is an availability circuit for the provider as a whole. HTTP 429 becomes provider_unavailable,
without a shared cooldown; the next capture can try again subject to local limits. Circuit state and
quota are process-local; multiple replicas need an explicit ingestion/coordination design.

Batch capture reads configured networks from the catalog and awaits all captures with Task.WhenAll,
each owning a DI scope and DbContext. Provider failures
are reported per chain, preserving successful observations. Request cancellation propagates. A persistence
failure causes HTTP 500; already committed snapshots remain. Neither HTTP requests nor the whole batch
hold a database transaction. POST is intentionally non-idempotent; after an ambiguous network/DB failure,
the caller must inspect history before deciding to retry.

## 5. Validation, mapping and operational boundaries

ApiController handles HTTP parameter binding errors. FluentValidation validators beside the application
commands/queries check required codes, positive IDs and pagination bounds (including offset overflow).
Handlers explicitly await ValidateAndThrowAsync before accessing the catalog, provider or repository.
API maps all validation failures to ValidationProblemDetails with public parameter names and a trace ID.
There is no automatic MVC validation integration or mediator pipeline. The handler's catalog lookup
rejects unsupported network combinations without a duplicate database query in a validator.
Domain constructors retain invariant checks; database constraints retain uniqueness and foreign keys.
Provider JSON is validated before storage. Mapping from entity to HTTP DTO is explicit, without a mapping
library for this single projection. System.Text.Json serializes the response envelope.

ProblemDetails unifies API errors. If content negotiation cannot find a problem writer (for example,
Accept: text/html), application exceptions still return application/problem+json with their original
validation details and trace ID. Logs contain network, attempt, status, elapsed time and request trace.
Liveness is independent of dependencies; readiness queries the actual snapshots table and never the provider.
CORS uses explicit configured origins. A concurrency limiter bounds incoming capture requests.

Migrations run as an explicit command/job before API startup. Multiple API replicas should not compete
to migrate the database. The API container runs as the built-in non-root user.

## 6. Deliberate limits and growth path

An opt-in SnapshotCollectionWorker (BackgroundService) invokes the existing CaptureAllSnapshots command
in a fresh async scope. It runs in the API host, preserving the same provider gate/circuit and storage
semantics as manual capture. A fixed delay after completion prevents overlapping automatic cycles or
catch-up bursts. Each cycle has a cooperative timeout, including persistence, and shutdown cancellation
propagates to all captures. Provider failures are reported individually; other failures are logged and
the next cycle runs after the configured delay. Already committed observations are never rolled back.
The worker is not started in migration-only mode. Options are validated on startup; defaults disable
automatic collection, with a 10-second initial delay, 600-second interval and 120-second cycle timeout.

This is a process-local scheduler: run it on exactly one instance. Scaling API replicas does not elect
a collection leader. A dedicated worker host and shared provider quota coordination are the growth path
when ingestion needs independent deployment; simply enabling this service on every replica would duplicate
observations and multiply provider requests. Manual calls can overlap with a background cycle and share
the local quota. There is no durable schedule, missed-run recovery, distributed lock or exactly-once claim.
Task.WhenAll starts all configured captures; a much larger catalog would need bounded concurrency. No queue, API Gateway, cache or Event Sourcing is
introduced. Health readiness is unchanged and does not imply recent data; snapshot age must be monitored
separately. One-second scheduling cannot overcome the configured hourly provider quota.

This exercise has no authentication requirement and is bound to localhost in Compose. Before exposing
capture publicly, add authentication/authorization and a shared quota policy. Retention, backup and
archival policies depend on operational requirements and are outside this exercise.

## Configuration ownership

The API host loads shared appsettings, environment-specific settings, Development User Secrets,
then environment/command-line overrides. Infrastructure owns typed provider and worker Options,
validated at startup. General settings contain no connection string and allow no CORS origins by default.
Development settings contain only a public demo database connection and localhost CORS origin.
Personal credentials belong in User Secrets; deployed credentials belong in the platform secret store.
Local Compose supplies its connection strings and CORS explicitly through environment variables.
Its optional .env is not loaded by .NET. appsettings.Local.json is neither loaded nor shipped in the
Docker build context. EF design-time commands require ConnectionStrings__Blockchain explicitly;
the application --migrate mode uses normal host configuration.
