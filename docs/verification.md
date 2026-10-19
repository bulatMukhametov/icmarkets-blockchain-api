# Verification record

Verified on 2026-10-18 and 2026-10-19 in the Windows development workspace.

## Completed

- .NET SDK 10.0.402; dependencies restored with NuGet vulnerability warnings treated as errors.
- Release solution build: zero warnings and errors.
- Latest Release tests: 67 passed, zero failed, zero skipped.
  - Unit: 7.
  - Integration: 45, including shared provider cooldown, background scheduling/lifecycle, circuit breaker transitions/concurrency, PostgreSQL migration/model
    consistency, complete payload preservation, currency/network isolation and keyset pagination.
  - Functional: 15, including validation errors with different Accept headers, automatic capture without POST, HTTP-to-PostgreSQL capture/history,
    concurrent partial collection and the circuit-open HTTP/batch contracts.
- The original 28-test suite used a temporary local PostgreSQL 17.11 instance through TEST_POSTGRES_CONNECTION.
  Each test fixture created and removed its own schema.
- The latest complete Release suite ran with RUN_DATABASE_TESTS=true and TEST_POSTGRES_CONNECTION
  unset: 67 passed, zero failed, zero skipped. Testcontainers provisioned PostgreSQL 17.11 Linux
  containers and cleaned them up successfully. The test host ran on Windows.
- Release publish succeeded with UseAppHost=false, as used by the Docker build.
- Published assembly applied migrations to a fresh database through --migrate.
- Published Kestrel host: liveness 200, readiness 200, five supported sources, Swagger JSON available.
- A real BTC mainnet capture through the published API returned 201. The stored snapshot was returned
  by GET history. Automated tests use controlled provider responses; live upstream calls are limited
  to the manual smoke checks described here.

## Docker verification

- Docker Desktop 4.95.0 installed with the WSL 2 backend; Docker Engine 29.8.2 and Compose 5.6.0.
- `docker compose config --quiet` and `docker compose up --build -d` succeeded.
- The multi-stage Dockerfile restored and published the application inside Linux, producing a
  linux/amd64 runtime image. The API runs as non-root UID 1654.
- PostgreSQL became healthy, the migration container exited with code 0, and the API started on
  localhost:5080. Liveness, readiness and Swagger JSON returned 200.
- A live collection succeeded for all five sources: eth/main, dash/main, btc/main, btc/test3 and
  ltc/main. Each saved snapshot was retrieved by ID with the expected provider payload name.
- An additional BTC capture returned 201 with a working Location. History returned the newest
  snapshot first; cursor pagination returned the preceding snapshot without duplication.
- Invalid page size returned 400; requesting a mainnet snapshot through the testnet route returned 404.
- `docker compose down` followed by `docker compose up -d` preserved the database volume and the
  snapshot payload unchanged. Migrations exited with code 0 again, readiness returned 200, and a new
  capture returned 201.
- The initial run exposed Npgsql's optional Kerberos library probe in the logs. Compose now explicitly
  sets GSS Encryption Mode=Disable for its local password-authenticated database. The recreated API
  and migration containers emitted neither that warning nor Error/Critical application logs during
  the repeat startup, read and capture checks.
- TRX reports and manual check results were generated under TestResults/docker/ (ignored by Git).

## Circuit breaker verification

- Added 20 integration cases and one HTTP contract test. FakeTimeProvider drives circuit windows,
  recovery pauses and attempt timeouts; recovery tests explicitly synchronize with the probe start.
- Verified the default minimum throughput/failure ratio, sampling expiry, sharing across clients and
  chains, open-circuit fast rejection without new quota reservations, a single concurrent half-open
  probe, successful recovery, failed/cancelled probe reopening, and admission recheck after the gate.
- Verified retry suppression and classification of HTTP errors, network failures, body-read errors,
  attempt timeouts, caller cancellation, invalid payloads and local quota exhaustion.
- HTTP tests verify 503 with Retry-After, continued history access and provider_circuit_open batch
  outcomes without extra upstream calls.
- Circuit breaker TRX reports are under TestResults/circuit-breaker-final/ (ignored by Git).
- Load capacity, freshness SLAs and multi-replica coordination have not been benchmarked or validated.

## Background collection verification (2026-10-19)

- Added ten integration cases using virtual time: disabled behavior, initial/fixed delays, separate
  disposed scopes, partial-success continuation, recovery after storage errors and cycle timeouts,
  non-overlap, shutdown cancellation during capture/startup delay, and invalid option rejection.
- Added an HTTP host test that enables the real hosted service and reads automatically populated
  histories for all five chains without issuing capture POST requests. Upstream responses and the
  repository are controlled test doubles in this functional test.
- The full Release suite passed all 60 tests with database tests enabled. Reports are under
  TestResults/background-worker/ (ignored by Git).
- The updated Linux Docker image built and published successfully.
- Enabled automatic collection temporarily in Compose: the worker logged 5/5 saved; GET history
  returned new snapshots 9..13 across all five chains without any capture POST. Readiness returned 200.
  Initial localhost polling with a two-second request timeout failed to observe the rows; subsequent
  IPv4 loopback requests with a longer timeout verified all rows. The API was then verified with
  collection disabled again, leaving PostgreSQL and the API running. Evidence is recorded in
  TestResults/background-worker/docker-smoke.json.

## Final review and naming (2026-10-19)

- Domain names now state the concepts explicitly: BlockchainNetwork and BlockchainNetworkSnapshot.
  Public application contracts and use-case messages/results have individual named files. Provider
  fetching, immediate repository persistence, request limiting and controller actions have explicit names.
- No pending relational model changes were detected after the CLR type rename; existing migrations,
  tables, columns and stored payloads are preserved. HTTP capture Location and history contracts passed.
- Four cooldown cases verify queued callers, shared limits across clients, delta/date Retry-After,
  final-attempt throttling, expiry, cancellation and quota preservation.
- Three HTTP cases verify validation details and trace ID for JSON, HTML and XML Accept headers.
- Release build completed with zero warnings/errors; all 67 tests passed with real PostgreSQL tests
  enabled. TRX reports are under TestResults/final-review/ (ignored by Git).
- Rebuilt and started the Linux image against the existing database. Readiness returned 200,
  pre-existing snapshot 9 remained readable, an HTML Accept request retained cursor validation details
  and trace ID with status 400, and a live BTC capture returned 201 (snapshot 14) with a working Location.
  Evidence is under TestResults/final-review/docker-smoke.json. Automatic collection remains disabled.
- GitHub Actions settings and workflow files were left unchanged at the user's request.

## Not verified

- A successful GitHub Actions run has not been confirmed. The local checks above do not establish
  the status of the remote workflow.

See README for reproducible commands.
