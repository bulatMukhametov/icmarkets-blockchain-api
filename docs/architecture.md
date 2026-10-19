# Architecture decisions

## 1. Clean Architecture and use-case folders

Domain has no framework dependencies. Application references Domain only and owns its ports.
Infrastructure implements persistence and the provider client. API owns HTTP contracts and the composition root.
There is one deployment unit and one database. Application folders group code by use case.

The domain is deliberately small: BlockchainNetwork identifies a supported currency/network pair;
BlockchainNetworkSnapshot is a saved provider response about that network at request-start time.
Snapshot means an observation, not a complete blockchain download. CreatedAt and Payload retain their
existing API/storage names and are documented on the domain type. Public application contracts and
use-case commands, queries and results have their own named files.
No artificial base entities, aggregates or per-currency service inheritance are needed.

## 2. CQRS and Repository

Commands capture new observations. Queries only read stored observations. Handlers implement distinct
command/query interfaces; CQRS does not require MediatR, separate databases or messaging.

The specialized repository exposes append, find and ordered history operations, without IQueryable
or a generic CRUD abstraction. EF Core DbContext is the Unit of Work; each append calls SaveChangesAsync.
There is no additional IUnitOfWork wrapper because no scenario coordinates multiple repositories.

## 3. Original payload, timestamps and pagination

Store the full successful response as PostgreSQL text, with metadata in separate columns.
This preserves unknown fields, large numeric literals and original formatting. PostgreSQL constraints
require an object document and a supported currency/network. HTTP responses wrap this data as a JSON object.

CreatedAt is the UTC time immediately before the first HTTP attempt, after waiting for the outbound gate.
It remains the first-attempt timestamp if a retry succeeds. Domain normalizes it to PostgreSQL's microsecond
precision. Each successful capture creates a new row, even if the payload did not change.

History uses keyset pagination ordered by CreatedAt DESC, Id DESC. Id is an identity bigint, resolving equal
timestamps. The opaque base64url cursor contains both keys and the chain; it is validated before querying.
The index matches the chain filter and ordering. A page fetches at most pageSize + 1 observations, without
tracking or an unbounded count query. A cursor is not signed and is not an authorization token.
Pagination is a live traversal, not a transactionally frozen export; newly arriving observations may be
seen on later pages if their request timestamps fall after the cursor in traversal order.

## 4. Provider resilience and concurrency

IHttpClientFactory manages connections. A singleton request gate spaces attempts and enforces a rolling
hourly budget across all currencies and retries in this process. Defaults leave headroom below the
documented free-tier ceiling. The gate does not wait for an hourly reset: exhausted budgets fail promptly.

HTTP attempts have a timeout and the complete capture has a bounded provider time budget, including gate
waiting and retry delays. Retries apply to network failures, timeouts, 408, 429 and 5xx. Short Retry-After
values are respected; long delays fail the request. Non-successful, invalid, oversized or wrong-chain
responses never become snapshots. Redirects are disabled. User-provided source URLs are not accepted.

A singleton Polly circuit breaker is shared across typed clients and all chains for the configured
provider. It samples individual HTTP attempts, including retries: by default at least five attempts
in 30 seconds with an availability failure ratio of 50% open it for 30 seconds. Network errors,
body-read I/O errors, per-attempt timeouts, 408 and 5xx count as failures. HTTP 429 and other 4xx,
payload validation, local gate failures and normal caller cancellation do not trip it. A cancelled
half-open probe reopens the circuit instead of claiming recovery or keeping the probe occupied.

Execution order is: total budget -> retry loop -> fast circuit check -> request gate -> authoritative
circuit admission -> attempt timeout -> send/read/validate. Checking admission after the gate prevents
waiting requests from sending after another attempt opens the circuit. Gate reservations made during
an opening race may conservatively remain charged to the hourly budget. Already sent attempts can finish.
An open circuit is never retried locally. After the break, Polly admits one probe to establish availability.
Transitions are logged. Polly types remain in Infrastructure; Application exposes a provider-circuit
exception, mapped to HTTP 503 with Retry-After or a per-chain provider_circuit_open batch outcome.

This is an availability circuit for the provider as a whole. Quota errors remain separate:
ProviderRequestLimiter shares the HTTP 429 Retry-After cooldown across all clients, even if no retries
remain. It supports delta and date headers, using one second if absent. A new cooldown can extend but
not shorten the existing one. New calls and calls still waiting for spacing fail without consuming
quota while the pause is active. Already admitted calls may finish. The original call retains bounded
short retries; long pauses fail promptly. Circuit state, cooldown and quota are process-local; multiple
replicas need an explicit ingestion/coordination design.

Batch capture starts five independent operations, each owning a DI scope and DbContext. Provider failures
are reported per chain, preserving successful observations. Request cancellation propagates. A persistence
failure causes HTTP 500; already committed snapshots remain. Neither HTTP requests nor the whole batch
hold a database transaction. POST is intentionally non-idempotent; after an ambiguous network/DB failure,
the caller must inspect history before deciding to retry.

## 5. Validation, mapping and operational boundaries

ApiController and DataAnnotations provide automatic HTTP parameter validation. Application validates
cursor semantics and page bounds, and the chain catalog prevents unsupported source combinations.
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
Batch concurrency is fixed by the five-entry catalog. No queue, API Gateway, cache or Event Sourcing is
introduced. Health readiness is unchanged and does not imply recent data; snapshot age must be monitored
separately. One-second scheduling cannot overcome the configured hourly provider quota.

This exercise has no authentication requirement and is bound to localhost in Compose. Before exposing
capture publicly, add authentication/authorization and a shared quota policy. Retention, backup and
archival policies depend on operational requirements and are outside this exercise.
