# Architecture decisions

## 1. Clean Architecture and use-case folders

Domain has no framework dependencies. Application references Domain only and owns its ports.
Infrastructure implements persistence and the provider client. API owns HTTP contracts and the composition root.
There is one deployment unit and one database. Application folders group code by use case.

The domain is deliberately small: a supported currency/network identity and an immutable observation.
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

ProblemDetails unifies API errors. Logs contain chain, attempt, status, elapsed time and request trace.
Liveness is independent of dependencies; readiness queries the actual snapshots table and never the provider.
CORS uses explicit configured origins. A concurrency limiter bounds incoming capture requests.

Migrations run as an explicit command/job before API startup. Multiple API replicas should not compete
to migrate the database. The API container runs as the built-in non-root user.

## 6. Deliberate limits and growth path

No scheduler, queue, API Gateway, cache or Event Sourcing is required by these use cases. If automated
capture becomes necessary, a worker can invoke the same command. Multiple replicas would need a shared
provider quota gate or a single ingestion worker; the current in-memory quota is per process and resets
on restart. Batch concurrency is fixed by the five-entry catalog.

This exercise has no authentication requirement and is bound to localhost in Compose. Before exposing
capture publicly, add authentication/authorization and a shared quota policy. Retention, backup and
archival policies depend on operational requirements and are outside this exercise.
