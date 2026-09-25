# Implementation Plan: Production Readiness: Pagination, Rate Limiting, Observability, End-User Sign-In

**Branch**: `002-production-readiness` | **Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-production-readiness/spec.md`

**Revision 2 (2026-09-25)**:
- Re-checked against constitution **v1.0.2**.
- Folds in the design-level findings from the first `/speckit-analyze` run:
  - T1, T2, U1: test isolation (research R-07)
  - G1: the trace link between the services (R-04)
  - A1: how SC-005 is measured (R-04)
  - D1: one place for the 429 `errorCode` (R-03)
  - U3: empty paging values (data-model §1)
  - E1: paging while data changes (contract, quickstart)
  - I1: naming
  - I2: the Swagger client id (R-05)
- C1 and X1 were resolved by the constitution amendment itself.

**Revision 3 (2026-09-25)**: Folds in the design-level findings from the second `/speckit-analyze` run:
- **P1**: the test capture records only spans that belong to a request or to a test (research R-07).
- **I1**: the FR-012 test design is split into two halves plus a manual check (R-04).
- **U1**: the parent span for the outgoing-call test comes from a source the host already records (R-04).
- **C1**: every package is in the R-06 table, with pinned versions: `Microsoft.Extensions.Diagnostics.Testing`
  10.10.0, and the Aspire dashboard image `13.5`.

**Revision 4 (2026-09-25)**:
- Designs the spec refinement from the security and API checklist (`checklists/security-api.md`): the changed
  FR-004, FR-006 to FR-010, FR-013, FR-016 and FR-018 to FR-021, and the new FR-024 to FR-027 (research R-08).
- Resolves the contract-level checklist items CHK015, CHK018, CHK019, CHK020, CHK021, CHK023, CHK024 and CHK028 in
  the contracts and the data model.
- User decisions: sign-out and per-account lockout are out of scope.

**Revision 6 (2026-09-25)**: Re-checked after `/speckit-clarify`. The sign-in page is English only, left to right (spec Clarifications). The design already used English messages, so the only change is that the page declares `lang="en" dir="ltr"` (research R-08, identity contract). The Constitution Check is unchanged: PASS.

**Revision 5 (2026-09-25)**: Folds in the third `/speckit-analyze` run:
- **I1**: the summary and research R-03 now say that only the token endpoint answers `429` in OAuth JSON. The
  sign-in form answers with an HTML page (R-08).
- **I2**: Technical Context lists every new package.
- **U1**: the Identity test project gets its own `LogCapture` (R-07).
- **C1**: the session lifetime is configurable, so the "session expires mid-sign-in" edge case can be tested with
  a short lifetime (R-08).

## Summary

This plan hardens the delivered phone book (feature 001) in four ways, each built as an independent slice:

1. **Paging (US1).** The v1 tag search returns `{ items, page, pageSize, totalCount, hasNext }`. This is a
   deliberate breaking change, chosen by the user. Ordering moves from memory into portable SQL
   (`ORDER BY last_name, first_name, id` with `LIMIT/OFFSET`), with byte-order collation on both providers:
   SQLite `BINARY`, PostgreSQL `LC_COLLATE=C`.
2. **Rate limiting (US2).** The built-in ASP.NET Core rate limiter is partitioned per `sub` claim or IP. The API
   answers `429` as ProblemDetails with `RateLimit.Exceeded`. The Identity token endpoint answers `429` in OAuth
   JSON (`temporarily_unavailable`), and the sign-in form answers with its HTML page and "Too many attempts. Try
   again in N seconds." (FR-009, research R-08). Health checks, API docs and Identity metadata are exempt, and there
   is no per-account lockout.
3. **Observability (US3).** OpenTelemetry traces and metrics come from ASP.NET Core, HttpClient and Npgsql,
   plus custom `PhoneBook.Application` and `PhoneBook.Persistence` spans that cover SQLite too, and a `PhoneBook`
   meter. Export is OTLP to an Aspire dashboard container. Personal data is removed from telemetry, and the
   ProblemDetails `traceId` becomes the W3C trace id.
4. **End-user sign-in (US4).** OpenIddict gains the authorization-code flow with mandatory PKCE, a minimal
   cookie-based login form with antiforgery protection, seeded users (password hashes via `PasswordHasher`), and
   a public first-party Swagger UI client.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (unchanged)

**Primary Dependencies**: existing stack (feature 001), plus:
- OpenTelemetry 1.19 (Extensions.Hosting, Instrumentation.AspNetCore and .Http, Exporter.OpenTelemetryProtocol)
- Npgsql.OpenTelemetry 10.0.3
- Microsoft.Extensions.Identity.Core 10.0.12 (`PasswordHasher` only)
- Microsoft.Extensions.Diagnostics.Abstractions 10.0.12 (`IMeterFactory`, Application layer; research R-07)
- from the shared framework, with no new package: rate limiting, cookie authentication and antiforgery
- for tests: OpenTelemetry.Exporter.InMemory 1.19.1 and Microsoft.Extensions.Diagnostics.Testing 10.10.0
  (`MetricCollector<T>`)
- local observability: the Aspire dashboard container `mcr.microsoft.com/dotnet/aspire-dashboard:13.5` (compose
  profile `observability`)

**Storage**: unchanged (in-memory SQLite by default, PostgreSQL behind the switch), plus:
- a new composite index `(normalized_tag, last_name, first_name, id)`
- a new **deployment requirement**: PostgreSQL uses `LC_COLLATE=C`
- users and rate-limit counters held in memory

**Testing**: xUnit v3, WebApplicationFactory, Testcontainers PostgreSQL (with `--locale=C`), Respawn and the
in-memory OpenTelemetry exporter. Integration tests run on both providers where the behaviour depends on the
database (paging).

**Target Platform / Project Type**: unchanged (two web hosts, Linux containers)

**Performance Goals**:
- one page of a 100k-contact tag in under 500 ms (SC-001)
- telemetry overhead within 5% (SC-005)

**Constraints**:
- no personal data in telemetry
- telemetry export must never fail or slow down requests
- rate-limit state stays per instance

**Scale/Scope**: 4 user stories, about 25 new or changed files, no new projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Gates come from constitution **v1.0.2**.

| Principle | Pre | Post | How the design complies |
|---|---|---|---|
| **I. DDD** | ✅ | ✅ | **Domain unchanged.** Paging is a read-side concern. Rate limiting and telemetry are cross-cutting host concerns. End users live in the Identity host, which is not part of the phone book domain. |
| **II. Result pattern** | ✅ | ✅ | **Results, not exceptions:** paging validation returns a `ValidationError`, and a login failure returns the form with a generic error. **No new catch sites:** telemetry export failures are handled inside the OTLP exporter. |
| **III. Two safety nets / error contract** | ✅ | ✅ | **API:** `429` is ProblemDetails with `errorCode` `RateLimit.Exceeded` and `traceId`. **Identity:** `429` and the login endpoints are OAuth/UI endpoints, exempt under v1.0.1. **`traceId`** becomes the W3C trace id (research R-04). |
| **IV. CQRS, portable SQL** | ✅ | ✅ | **Paging stays on the Dapper read side.** The COUNT and page queries use only portable SQL (`LIMIT/OFFSET`). **Identical ordering** on both providers comes from collation configuration, not provider-specific SQL (R-02). |
| **V. Test discipline** | ✅ | ✅ | Tests are written first for every story. **Paging** is tested on SQLite **and** PostgreSQL. **Rate limiting, telemetry (including a scan for personal data) and the full PKCE flow** get integration tests. Domain unit tests are unaffected. **Isolation** (R-07): each rate-limit test gets a fresh host, and PostgreSQL telemetry tests run in the `Postgres` collection, so no counters or databases are shared across parallel tests. Every acceptance scenario maps to a test. **Full suite at every checkpoint.** |
| **VI. Secure by default** | ✅ | ✅ | **PKCE is mandatory** and authorization codes are single-use. **Login** has antiforgery protection, a local-only `ReturnUrl` and generic error messages. **Passwords** are held only as hashes. **Credential-accepting endpoints are rate-limited per source address**, as v1.0.2 now requires: the `token` policy covers `/connect/token` and `POST /account/login`, and the limiter runs before OpenIddict handles the request (R-03). **Dev users and secrets** are marked DEV-ONLY. **Business routes** still require policies; the only new anonymous endpoints are `/connect/authorize` and `/account/login` on the Identity host, which v1.0.2 explicitly allows. **Hardening** (R-08): only the S256 proof-key method is accepted; tokens issued from a replayed code are revoked; return addresses must match exactly; lifetimes are 5 min for codes, 1 h for tokens and 15 min for the session; the cookie is HttpOnly, SameSite=Lax and Secure over HTTPS; timing is equalised for unknown users; there are no seeded users outside Development; nothing personal is logged. |
| **VII. Spec-driven, documented** | ✅ | ✅ | The spec's clarifications are recorded, the breaking change is flagged in the contract and README, and research R-01 to R-07 record the decisions. **AI assistance** is disclosed in the README cover letter, with no commit trailers, as workflow gate 6 in v1.0.2 requires. |

**Technology & Architecture Constraints**:
- **Licensing:** every new package is stable and Apache, MIT or PostgreSQL licensed. The beta EF Core
  OpenTelemetry instrumentation is deliberately avoided (R-04).
- **Portable SQL:** preserved.

**Result: PASS (re-checked against v1.0.2).** No violations. One deployment requirement is added (PostgreSQL `LC_COLLATE=C`), and the breaking
v1 change is approved by the user and documented.

## Project Structure

### Documentation (this feature)

```text
specs/002-production-readiness/
├── spec.md · plan.md · research.md · data-model.md · quickstart.md
├── contracts/phonebook-api-changes.openapi.yaml · contracts/identity-signin.md
├── checklists/requirements.md
└── tasks.md                      # /speckit-tasks
```

### Source Code (changes to the existing layout)

```text
src/PhoneBook.Application/
├── Abstractions/Paging/PagedResponse.cs, PagingDefaults.cs           # US1
├── Abstractions/Telemetry/PhoneBookTelemetry.cs (ActivitySources, names), PhoneBookMetrics.cs (IMeterFactory counters; R-07)   # US2/US3
├── Abstractions/Behaviors/LoggingBehavior.cs  (+ request span)        # US3
├── Abstractions/Data/ISqlConnectionFactory.cs  (+ ProviderName for db.system)   # US3
├── Contacts/GetByTag/*  (Page/PageSize, COUNT + page SQL, validator)  # US1
└── Contacts/EventHandlers/*  (+ contact counters)                     # US3
src/PhoneBook.Infrastructure/
├── Persistence/DatabaseInitializer.cs  (new composite index)          # US1
├── Persistence/WriteDbContext.cs  (+ persistence span)                # US3
└── DependencyInjection.cs
src/PhoneBook.Api/
├── Endpoints/Contacts/GetContactsByTag.cs  (page, pageSize)           # US1
├── Infrastructure/RateLimiting/RateLimitingSetup.cs, RateLimitOptions.cs   # US2
├── Infrastructure/Telemetry/TelemetrySetup.cs (OTel + url.query scrubbing) # US3
├── Infrastructure/Swagger/SwaggerSetup.cs  (+ authorizationCode flow, PKCE; client id phonebook-swagger-ui) # US4
├── Infrastructure/TraceIds.cs  (W3C trace id for ProblemDetails)       # US3
└── Program.cs  (UseRateLimiter order, traceId change)
src/PhoneBook.Identity/
├── Users/IdentityUser.cs, StableUserId.cs, InMemoryUserStore.cs, *Options.cs  # US4
├── Endpoints/AuthorizeEndpoint.cs, AccountEndpoints.cs (login form; HTML 429 page)   # US4
├── Endpoints/TokenEndpoint.cs (+ authorization_code branch)           # US4
├── Seeding/IdentitySeeder.cs (+ public client phonebook-swagger-ui)   # US4
├── RateLimiting + Telemetry setup                                     # US2/US3
└── Program.cs, appsettings*.json
tests/PhoneBook.Api.IntegrationTests/
├── Contacts/Queries/GetContactsByTag*  (updated to paged; + paging tests on both providers)
├── Infrastructure/TelemetryCapture.cs  (in-memory exporters, attachable to any factory; R-07)
├── RateLimiting/*  (a fresh tiny-limit host per test; R-07)
└── Telemetry/*  (spans, metrics, PII scan; PostgreSQL variants in the Postgres collection; R-07)
tests/PhoneBook.Identity.IntegrationTests/
├── Infrastructure/IdentityFactory.cs  (unsealed; rate limit overridable; R-07), PkceClient.cs
├── Infrastructure/LogCapture.cs  (its own copy; this project does not reference Api.IntegrationTests; R-07)
├── AuthorizationCodeFlowTests.cs, AccountLoginTests.cs, SeededUsersEnvironmentTests.cs  (PKCE walk-through, negatives, FR-027)
├── TokenRateLimitTests.cs  (a fresh host per test)
├── TelemetryTests.cs
└── EndToEndTokenToApiTests.cs  (+ end-user token)
tests/PhoneBook.Api.IntegrationTests/Telemetry/OutgoingCallTracingTests.cs  (the API half of the FR-012 link; R-04)
docker-compose.yml  (postgres --locale=C; observability profile: aspire-dashboard:13.5)
```

**Structure Decision**: no new projects. Each concern lands in the layer that owns it, following the feature-001
architecture. The architecture tests stay green: rate limiting and telemetry live in the hosts, and the Application
layer uses `System.Diagnostics` (ActivitySource and Meter) only, which is BCL, not a provider driver.

## Complexity Tracking

| Added complexity | Why needed | Simpler alternative rejected because |
|---|---|---|
| PostgreSQL must use `LC_COLLATE=C` | Byte-order sorting in SQL, the same on both providers, is required for correct paging (R-02) | Sorting in memory does not scale, and provider-specific `COLLATE` clauses would break constitution IV |
| Custom persistence spans alongside Npgsql's native tracing | SQLite emits no database spans, and the EF Core instrumentation is beta-only | Relying on the beta package conflicts with the stable-only licensing and quality policy |
| A hand-written in-memory user store with `PasswordHasher` instead of ASP.NET Core Identity | The spec scopes users to seeded configuration only | Full Identity adds EF stores, a user manager and a UI, none of which the spec needs |
| A breaking change to v1 | The user explicitly chose to replace in place (spec FR-006) | A v2 endpoint was offered and declined |
