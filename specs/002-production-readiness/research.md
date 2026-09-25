# Research: Production Readiness

**Feature**: `002-production-readiness` | **Date**: 2026-09-25 | **Plan**: [plan.md](./plan.md)

This feature builds on the delivered feature 001 (see [`../001-phonebook-management/research.md`](../001-phonebook-management/research.md),
R-01 to R-18). Everything here must keep constitution v1.0.1 intact, and in particular Principle IV: the SQL must
run unchanged on every provider.

---

## R-01 Paging model and response shape

- **Decision**: page-number paging (`page` ≥ 1, `pageSize` 1–200, default 50). The response replaces the v1
  tag-search body **in place**:

  ```json
  { "items": [ ContactResponse ], "page": 1, "pageSize": 50, "totalCount": 250, "hasNext": true }
  ```

  This is a **deliberate breaking change**, chosen by the user (spec FR-006). v1 is replaced rather than
  versioned, so the change is flagged as breaking in the API contract and in the README.
- **Rationale**: page numbers are what the spec asks for (the page number appears in the response), are simple
  for clients, and give a deterministic order once the tie-breaker below is added.
- **Alternatives**:
  - continuation-token (keyset) paging: faster for very deep pages, but harder to use and not needed at this
    scale
  - a v2 endpoint: rejected by the user

## R-02 Ordering must move into SQL, portably ⚠️ key decision

Feature 001 sorted the tag results **in memory** with `StringComparer.Ordinal`, because SQLite (BINARY) and
PostgreSQL (the database locale) collate text differently (001 R-07, finding P1). Paging cannot work that way:
sorting 100k rows in memory for every page defeats SC-001.

- **Decision**: `ORDER BY last_name, first_name, id` in SQL, with **byte-order collation on both providers**:
  - SQLite: the default `BINARY` collation compares UTF-8 bytes, which is Unicode code-point order.
  - PostgreSQL: the database is created with `LC_COLLATE=C`, which compares bytes the same way. This applies to
    Testcontainers (`POSTGRES_INITDB_ARGS=--locale=C --encoding=UTF8`) and to the compose `postgres` service.
    It is recorded as a **deployment requirement**.
  - `id` is the final tie-breaker, so pages are deterministic even when names are equal.
  - The SQL stays identical on both providers (constitution IV). Ordinal UTF-16 order and code-point order
    differ only for characters above U+FFFF compared with U+E000–U+FFFF, which is irrelevant for names and is
    documented.
- **Count and page**: two parameterised statements, both portable:
  - `SELECT COUNT(*) FROM contacts WHERE normalized_tag = @Tag`
  - `SELECT … WHERE normalized_tag = @Tag ORDER BY last_name, first_name, id LIMIT @Take OFFSET @Skip`
- **Index**: replace `ix_contacts_normalized_tag` with
  `ix_contacts_tag_order (normalized_tag, last_name, first_name, id)`, created with portable DDL in
  `DatabaseInitializer`.
- **Alternatives rejected**:
  - in-memory sort: does not scale
  - `COLLATE "C"` in SQL: not valid on SQLite without registering a custom collation on every connection
  - a precomputed sort-key column: extra write-side complexity

## R-03 Rate limiting

- **Decision**: use ASP.NET Core's built-in rate limiter (`Microsoft.AspNetCore.RateLimiting`, part of the shared
  framework, so no new package), with **fixed-window, partitioned** limiters.
  - **Phone book API, policy `api`**: the partition key is the `sub` claim when authenticated (the `client_id`
    for client credentials, the user id for end users), otherwise the remote IP. Default 100 requests per 60 s.
  - **Identity host, policy `token`**: on `POST /connect/token` and on the login form POST
    (`/account/login`, brute-force protection). The partition key is the remote IP. Default 10 per 60 s.
  - **Pipeline order**: `UseAuthentication` → `UseRateLimiter` → `UseAuthorization`, so the `sub` claim is known
    when the partition is chosen. `/health/*` endpoints call `.DisableRateLimiting()`.
  - **No queueing** (`QueueLimit = 0`): requests over the limit are rejected at once (spec edge case).
- **Rejection**:
  - **API**: `OnRejected` writes a `429` ProblemDetails with a `Retry-After` header from `RetryAfter` lease metadata
    (constitution III). `errorCode = "RateLimit.Exceeded"` and `traceId` are added in **one place**: the
    `CustomizeProblemDetails` status switch gains `429 → RateLimit.Exceeded`, so every 429 is covered the same way
    (analyze D1).
  - **Identity token endpoint**: a `429` with `Retry-After` and the OAuth-style body
    `{"error":"temporarily_unavailable","error_description":"Too many requests. Retry later."}`
    (constitution III exempts OAuth endpoints).
  - **Identity sign-in form** (`POST /account/login`): a `429` with `Retry-After` and the **HTML sign-in page**
    carrying "Too many attempts. Try again in N seconds." It is never JSON. This is superseded in detail by R-08
    (FR-009, analyze I1).
- **Identity pipeline order** (constitution v1.0.2, Principle VI):
  - The order is `UseRouting` → `UseCors` → **`UseRateLimiter`** → `UseAuthentication` → `UseAuthorization`.
  - OpenIddict handles `/connect/token` requests inside the authentication middleware, and answers errors such as
    `invalid_client` there. A limiter placed after `UseAuthentication` would never count credential guessing.
  - `UseRouting` comes first so the endpoint's rate-limit metadata is visible.
  - The partition is the IP address only, so no authenticated identity is needed.
- **Source address**: the partition key uses `HttpContext.Connection.RemoteIpAddress`. A reverse proxy deployment
  needs `ForwardedHeaders` configured, or all clients share one partition. This is documented as a deployment
  note and is out of scope, since compose has no proxy.
- **Configuration**: `RateLimiting:Api:{PermitLimit,WindowSeconds}` and
  `RateLimiting:Token:{PermitLimit,WindowSeconds}`, read at runtime through options (FR-011).
- **Test impact**: the feature-001 load tests (for example 100 mixed requests from one client) would hit the
  default limits. The test factories therefore set high limits by default, and dedicated rate-limit tests use a
  factory with tiny limits (for example 5 per 10 s).
- **Per-instance state**: in memory, as the spec assumes. A distributed limiter (for example Redis) is future
  work.
- **Alternatives**: the `AspNetCoreRateLimit` package is older, external and less maintained than the built-in
  limiter; a reverse-proxy limiter (nginx or YARP) is outside the application and harder to test.

## R-04 Observability: OpenTelemetry

- **Decision**: OpenTelemetry .NET 1.19 (stable), set up in both hosts:
  - **Tracing**:
    - `OpenTelemetry.Instrumentation.AspNetCore` for incoming requests
    - `OpenTelemetry.Instrumentation.Http` for outgoing calls, such as the API fetching the Identity discovery
      document and JWKS
    - `Npgsql.OpenTelemetry` for PostgreSQL spans; Npgsql is instrumented natively and stable, and this covers
      both EF Core and Dapper
    - the custom `ActivitySource` **`PhoneBook.Application`**: a span per MediatR request, added in the
      `LoggingBehavior`
    - the custom source **`PhoneBook.Persistence`**: spans around `IUnitOfWork.SaveChangesAsync` and the Dapper
      tag query. These give database spans on **SQLite too**, because Microsoft.Data.Sqlite emits none.
  - **Metrics**:
    - ASP.NET Core built-in meters (`http.server.request.duration`, with status and route)
    - `Microsoft.AspNetCore.RateLimiting` meters
    - the custom `Meter` **`PhoneBook`**, with the counters `phonebook.contacts.created`, `.updated` and
      `.deleted` (incremented in the existing domain-event handlers) and `phonebook.ratelimit.rejections`
      (incremented in `OnRejected`, tagged with the policy)
  - **Export**: OTLP (`OpenTelemetry.Exporter.OpenTelemetryProtocol`), enabled by `Telemetry:OtlpEndpoint`. When
    it is empty, export is off (FR-015). The batch exporter is asynchronous and drops on failure, so it never
    blocks requests (FR-017).
- **EF Core instrumentation**: `OpenTelemetry.Instrumentation.EntityFrameworkCore` is **beta only**, so it is
  deliberately **not used** (the stable-only policy). The custom persistence spans and Npgsql's native spans cover
  FR-012.
- **Link between the services (FR-012)**:
  - The phone book API calls the Identity host only to fetch the discovery and JWKS documents: at start-up, then
    whenever the cached metadata is refreshed. It does not call it once per request.
  - The HttpClient instrumentation injects `traceparent` into those calls, and the Identity host's ASP.NET Core
    instrumentation continues the same trace. That is the "link" FR-012 requires, and it is what the design
    delivers.
  - End-user and client requests reach the two hosts independently. Each of those is its own trace, correlated by
    `sub` and `client_id` in the logs, not by trace id.
  - **Test** (analyze G1; revised after the second analyze run, I1 and U1):
    - A single in-process test cannot show the whole link. The feature-001 end-to-end test gives the API the
      Identity configuration directly, and in-process `TestServer` handlers skip .NET's HTTP diagnostics, so no
      client span or `traceparent` would ever appear. The link is therefore proved in two halves plus a manual
      check.
    - **API half** (`OutgoingCallTracingTests`):
      - The test starts a parent span from `PhoneBookTelemetry.Application`, a source the host's tracer already
        records.
      - Inside it, a client from the host's `IHttpClientFactory`, using the default socket handler, calls an
        unreachable local address.
      - The recorded HTTP client span is a child of that parent: same `TraceId`, and parent id equal to the
        parent's span id. This proves outgoing calls are traced and carry the trace context.
    - **Identity half** (`TelemetryTests`): a request with a W3C `traceparent` produces a server span that
      continues that trace.
    - **Live check**: quickstart #11 shows the API's discovery and JWKS calls and the Identity server spans in one
      trace in the Aspire dashboard.
- **Measuring SC-005** (analyze A1):
  - Comparing latency within 5% between two in-process test hosts is too noisy for a hard CI assertion.
  - **Automated**: with the OTLP exporter pointed at an unreachable address, every request returns its normal
    status, and no request is slower than 1 s. This proves no blocking.
  - **Manual**: the 5% comparison is quickstart scenario #13 against the running compose stack, with and without
    the dashboard container.
- **Trace id in errors (FR-013)**: the ProblemDetails `traceId` changes from `Activity.Current?.Id` (the full
  W3C `traceparent`) to **`Activity.Current?.TraceId.ToHexString()`**, the 32-hex trace id that telemetry back
  ends index. It falls back to `HttpContext.TraceIdentifier` when there is no activity. Feature-001 tests only
  check that the field is present, so nothing breaks.
- **Personal data (FR-016, SC-006)**:
  - The ASP.NET Core instrumentation's `EnrichWithHttpRequest` **removes `url.query`**, which contains `?tag=…`,
    and records `url.path` only. Routes are templated (`/api/v1/contacts/{id}`).
  - Npgsql spans keep `db.statement` (parameterised SQL, no values) and never record parameter values.
  - Custom spans hold only request type names, error codes and counts, never field values.
  - Metric tags use only route templates, status codes and policy names.
  - An integration test runs traffic containing distinctive names, phone numbers and tags, captures every span
    and metric with the in-memory exporter, and asserts none of those values appear.
- **Local collector**: the **.NET Aspire dashboard** container (`mcr.microsoft.com/dotnet/aspire-dashboard`,
  MIT-licensed, an OTLP receiver with a trace and metric UI) under a new compose profile `observability`.
  Choosing and hosting a production back end is out of scope (spec assumption).
- **Alternatives**: Serilog-only tracing (not a standard); `Microsoft.ApplicationInsights` (tied to one vendor);
  the Jaeger and Prometheus containers (two tools, where the Aspire dashboard shows both signals in one).

## R-05 End-user sign-in: authorization code + PKCE with OpenIddict

- **Decision**: extend `PhoneBook.Identity`:
  - `AllowAuthorizationCodeFlow()` plus `RequireProofKeyForCodeExchange()` (server-wide PKCE) and
    `SetAuthorizationEndpointUris("connect/authorize")`, with `EnableAuthorizationEndpointPassthrough()`.
  - **Login UI**: a minimal Razor-free HTML form served by minimal API endpoints `GET/POST /account/login`,
    backed by **cookie authentication** (`CookieAuthenticationDefaults`), with **antiforgery** enabled
    (`UseAntiforgery`).
    - A failed login returns the same page with the generic message "Invalid username or password." (FR-021). No
      exception is thrown (constitution II).
  - **Authorize endpoint**:
    - Anonymous users get `Challenge(cookie)` and are redirected to the login page with `ReturnUrl`.
    - Authenticated users get a principal with `sub` = user id, `name`, and scopes = the requested scopes that
      the user is permitted to have. Scopes the user lacks are silently dropped, which is standard OAuth scope
      down-scoping.
    - The principal gets the `phonebook-api` resource and access-token destinations. The API's existing
      `Contacts.Read` and `Contacts.Write` policies then apply unchanged (FR-020).
  - **Consent**: the Swagger UI is registered as a first-party client with `ConsentType = Implicit`, so there is
    no consent screen (spec assumption).
  - **Public client** `phonebook-swagger-ui`: `ClientType = Public` with no secret. Its permissions are the
    authorization and token endpoints, the authorization-code grant, response type `code` and the two scopes,
    and it has the `Requirements.Features.ProofKeyForCodeExchange` requirement. Its redirect URIs are
    `https://localhost:7001/swagger/oauth2-redirect.html` and `http://localhost:7001/swagger/oauth2-redirect.html`
    (compose), both taken from configuration.
  - **Users**:
    - They are defined under `Identity:Users` (`UserName`, `Password`, `DisplayName`, `Scopes[]`), with
      development values marked DEV-ONLY.
    - At start-up an `InMemoryUserStore` hashes each password with **`PasswordHasher<TUser>`** from
      `Microsoft.Extensions.Identity.Core` (PBKDF2), and only the hashes are kept in memory.
    - Verification uses `VerifyHashedPassword`.
    - Full ASP.NET Core Identity (EF stores, user manager and UI) is deliberately not used, because the spec
      puts account management out of scope.
- **API side**: nothing changes except that the Swagger UI adds an OAuth2 `authorizationCode` flow
  (`authorizationUrl`, `tokenUrl`) next to `clientCredentials`, with `OAuthUsePkce()` and
  `OAuthClientId("phonebook-swagger-ui")`.
  - Swagger UI pre-fills **one** client id for every flow, so the pre-filled id becomes the secret-less public
    client.
  - Client-credentials users of the Swagger UI now type `phonebook-swagger` and its DEV-ONLY secret.
  - This is recorded in the README, the compose header comment and a note in the feature-001 quickstart
    (analyze I2).
- **Token contents**: code-flow tokens are signed JWTs like client-credentials tokens: `aud=phonebook-api`, and
  `scope` limited to what the user is permitted.
- **Authorization codes**: single-use by default in OpenIddict (FR-019), because the token store records
  redemption.
- **Tests**: a full browser-less walk-through with `WebApplicationFactory`:
  1. `GET /connect/authorize?...&code_challenge=…` returns 302 to `/account/login`.
  2. `GET /account/login` returns the form and the antiforgery token.
  3. `POST /account/login` returns 302 back to authorize with a cookie.
  4. Authorize returns 302 to the redirect URI with `code`.
  5. `POST /connect/token` with `code_verifier` returns a token.
  - Negative cases: a missing or wrong `code_verifier` is rejected, and a code reused twice is rejected.
  - An end-to-end test calls the API with the user's token.
- **Alternatives**: full ASP.NET Core Identity (unneeded weight), Razor Pages for the login (a new UI framework
  for one form), the implicit flow (deprecated by OAuth 2.1).

## R-06 Packages (all stable and permissively licensed)

| Package | Version | Licence |
|---|---|---|
| OpenTelemetry.Extensions.Hosting | 1.19.1 | Apache-2.0 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.19.0 | Apache-2.0 |
| OpenTelemetry.Instrumentation.Http | 1.19.0 | Apache-2.0 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.19.1 | Apache-2.0 |
| OpenTelemetry.Exporter.InMemory (tests) | 1.19.1 | Apache-2.0 |
| Npgsql.OpenTelemetry | 10.0.3 | PostgreSQL |
| Microsoft.Extensions.Identity.Core | 10.0.12 | MIT |
| Microsoft.Extensions.Diagnostics.Abstractions (`IMeterFactory`, Application) | 10.0.12 | MIT |
| Microsoft.Extensions.Diagnostics.Testing (`MetricCollector<T>`, tests) | 10.10.0 | MIT |
| Aspire dashboard container (`mcr.microsoft.com/dotnet/aspire-dashboard`, compose `observability` profile) | 13.5 | MIT |
| Rate limiting, antiforgery, cookie auth | shared framework | MIT |

## R-07 Test isolation for rate limiting and telemetry (analyze T1, T2, U1)

- **Rate-limit tests get a fresh host per test.**
  - Rate-limit counters live in the host's memory, and xUnit shares an `IClassFixture` or collection fixture
    across all tests in a class.
  - A shared host would carry one test's used allowance into the next, especially on the Identity host, where
    every in-process request has the same partition (`ip:unknown`).
  - So `RateLimitedApiFactory` and `RateLimitedIdentityFactory` are **created in each test class instance's
    constructor and disposed in `DisposeAsync`**. xUnit builds a new instance per test, so each test gets new
    counters.
  - API tests also use a distinct `sub` per test (`X-Test-Sub`), to be safe.
  - The window is short (2 s) where a test waits for `Retry-After`.
- **The Identity test host can be extended.**
  - `IdentityFactory` is unsealed.
  - Its token limit is a `protected virtual int TokenPermitLimit => 1_000_000`, overridden to `3` by
    `RateLimitedIdentityFactory`.
  - This keeps the default of high limits without blocking the subclass.
- **PostgreSQL telemetry tests share the collection's database.**
  - Collections run in parallel, and Respawn resets the whole `phonebook` database.
  - A second PostgreSQL factory outside the `Postgres` collection would wipe data under running tests.
  - So PostgreSQL telemetry and personal-data tests run **in `[Collection("Postgres")]`**, using a
    `TelemetryCapture` attached to the collection's `PhoneBookApiFactory` through `ConfigureTestServices`.
    The capture is always attached, which is harmless.
  - They filter captured spans by the `TraceId` of their own requests.
  - SQLite telemetry tests use their own factory with a unique in-memory database.
- **Compile-first stubs.**
  - In C#, "tests fail first" for a changed signature means the whole test project fails to build, which blocks
    running every other test.
  - Where a test needs a new public type or signature (`PagedResponse<T>`, the extended
    `GetContactsByTagQuery`), the type and signature are added **first**, returning the old behaviour or an empty
    result. Then the tests are written and fail on behaviour, and then the implementation is completed.
  - This keeps Principle V's "fail first" meaningful: a test fails because of an assertion, not a compile error.
- **Per-host metrics through `IMeterFactory`** (found while writing tasks revision 2, M1):
  - A static `Meter` is process-wide. Every test host's `MeterProvider` subscribes to it by name, so counters from
    parallel test hosts would mix, and "exactly 1 created" assertions would be flaky.
  - So `PhoneBookMetrics` (Application) and `IdentityMetrics` (Identity) create meter `PhoneBook` from the DI
    `IMeterFactory` and are registered as singletons.
  - Tests measure with `MetricCollector<T>` (`Microsoft.Extensions.Diagnostics.Testing`), bound to the host's own
    factory.
  - The `ActivitySource`s stay static, because tests filter spans by trace id.
  - The telemetry names in data-model §3 do not change.
- **The test capture keeps only relevant spans** (analyze P1):
  - Each test host's tracer receives spans from every `ActivitySource` in the process, including the per-row
    Npgsql spans of the 110,000-row bulk insert in the performance test. Keeping them all would cost hundreds of
    MB per host and add overhead during timed queries.
  - The capture therefore uses a **filtering processor**. It keeps an activity only when:
    - it is an ASP.NET Core server span, or
    - it has a parent (so it belongs to a request or to a test-started span), or
    - it comes from `PhoneBook.Application` or `PhoneBook.Persistence`.
  - Root spans from other sources, such as Npgsql commands issued directly by test seeding code, are dropped
    before they are stored.
  - The personal-data scan still sees every request's spans, because every request has a server span.

## R-08 Sign-in and API hardening (from the security and API checklist; spec FR-004 to FR-027 refinements)

Each item gives the decision and then the reason. Items marked *(user)* are the user's choices.

**Rate limiting**

- **429 wins over 401 and 403** (FR-007): `UseRateLimiter` sits before `UseAuthorization` on the API and before
  `UseAuthentication` on Identity, so a caller over the limit is rejected before the auth outcome is known. Counting
  failed authentication attempts is the point of brute-force protection.
- **Only `Retry-After`** (FR-009): it is in whole seconds, rounded up, and at least 1. There are no `RateLimit-*`
  headers, because those IETF headers are still a draft; they are left as future work.
- **Token-endpoint 429 body** (FR-009, CHK018): `{"error":"temporarily_unavailable","error_description":"Too many
  requests. Retry later."}`.
  - RFC 6749 §5.2 defines no token-endpoint error for throttling. `temporarily_unavailable` (RFC 6749 §4.1.2.1) is
    the closest standard code, and HTTP `429` plus `Retry-After` carry the real signal.
  - This is recorded in the identity contract as a documented extension.
  - Alternative rejected: `slow_down`, which belongs to the device flow (RFC 8628).
- **Sign-in form 429** (FR-009): the Identity limiter's `OnRejected` checks the endpoint.
  - For `POST /account/login` it renders the sign-in page with status `429`, a `Retry-After` header and the
    message "Too many attempts. Try again in N seconds."
  - For every other endpoint it returns the OAuth JSON body.
- **Exempt endpoints** (FR-010): `/health/*`, `/swagger/*` (API), and `/.well-known/*` (discovery and JWKS on
  Identity). These have no rate-limit policy attached, and health checks call `DisableRateLimiting()` explicitly.
- **No per-account lockout** *(user)*: only the per-IP `token` policy, shared by `/connect/token` and
  `POST /account/login` (FR-008).

**Sign-in (OpenIddict)**

- **S256 only** (FR-018): configure the OpenIddict server so `CodeChallengeMethods` contains only `S256`, removing
  `plain` if a default adds it. Discovery then advertises only `S256`, and `plain` is refused with
  `invalid_request`.
- **Revocation on code replay** (FR-019): keep OpenIddict's token and authorization storage enabled, which is the
  default. When a redeemed code is presented again, OpenIddict rejects it with `invalid_grant` and revokes the
  tokens tied to the same authorization. A test asserts that the first access token is then refused by
  introspection or is marked revoked in the store.
- **Missing `code_verifier`** (CHK015): the contract states `invalid_grant`, which is what OpenIddict returns when
  the code carries a challenge. As in feature 001, the test records the observed code, and the contract is
  corrected if it differs.
- **Down-scoping and access denied** (FR-020): granted scopes are the requested scopes that the user also holds.
  If that leaves nothing, or `scope` is absent, the endpoint calls `Forbid` with `error=access_denied`. OpenIddict
  then redirects to `redirect_uri?error=access_denied&state=…`.
- **Timing** (FR-021): for unknown users the user store verifies the password against a fixed dummy PBKDF2 hash,
  so both failure paths cost one hash verification. The test compares the medians of 10 attempts each and allows
  a ±50% band, because it only needs to show there is no order-of-magnitude difference.
- **Exact return addresses** (FR-024): OpenIddict compares `redirect_uri` to the registered URIs with an exact
  string match, which is its default behaviour. `ReturnUrl` after login must be local (`/`-rooted, not `//` or
  `/\`). Anything else falls back to `/`. An unknown client or unregistered redirect gets an OpenIddict error
  response on the Identity host and is never redirected.
- **Cookie** (FR-025):
  - `HttpOnly`
  - `SameSite=Lax`: not sent on cross-site sub-requests, but still sent on top-level navigation, which the
    authorize redirect needs
  - `SecurePolicy=SameAsRequest`, which gives Secure on HTTPS
  - `ExpireTimeSpan=15 min` with `SlidingExpiration=false`
- **Page language** (spec Clarifications, 2026-09-25): the sign-in page is English only, left to right (`<html lang="en" dir="ltr">`). The exact messages are "Invalid username or password." and "Too many attempts. Try again in N seconds." There are no localisation resources. Every form field has a `<label>`, which is basic accessibility.
- **Antiforgery**: the minimal API form binding validates the token (`UseAntiforgery`). A missing or invalid token
  gives `400`.
- **Lifetimes** (FR-026): `SetAuthorizationCodeLifetime(5 min)` and `SetAccessTokenLifetime(1 h)`, which is also
  the client-credentials value, so FR-023 is unchanged.
  - All three lifetimes (code, access token, and the 15-minute **session** cookie) live in `IdentitySettings`.
  - They are applied through options at runtime, so tests can shorten them.
  - The "session expires between signing in and returning to the application" edge case is tested with
    `Identity:SessionLifetime=00:00:02`: sign in, wait 3 s, then authorize → `302` back to `/account/login`
    (analyze C1).
- **No sign-out** *(user)*: sign-out is out of scope and listed as future work in the README.
- **Seeded users only in Development** (FR-027):
  - `Identity:Users` is empty in `appsettings.json` and populated only in `appsettings.Development.json`.
  - Outside Development, a start-up check logs a warning and ignores any configured users if a user entry
    carries a plain-text password.
- **Password hashing**: ASP.NET Core `PasswordHasher` v3 uses PBKDF2 with HMAC-SHA512 and 100,000 iterations,
  with a salt per hash. This is recorded against the OWASP recommendation as the "deliberately slow" criterion.

**Personal data in telemetry and logs** (FR-016)

- **Traces**: the enrichers remove `url.query` **and `client.address`** from server spans (and `client.address` is
  never added by hand). Request bodies are never captured, because no body enricher is registered.
- **Logs**:
  - Serilog request logging keeps its default template (method, path, status, elapsed). Path templates carry ids,
    not personal values.
  - Application log messages carry only request names and error codes.
  - OpenIddict and ASP.NET Core stay at `Warning`, as they already are in both hosts' `appsettings.json`.
  - The sign-in endpoints log outcomes without user names.
- **Test**: the personal-data scan (SC-006) also covers logs. Tests register an in-memory Serilog `ILogEventSink`
  in DI, which `ReadFrom.Services` picks up, and scan rendered messages and properties for the same distinctive
  values, plus end-user user names, passwords, client secrets and `127.0.0.1` / `::1`.

**API contract details**

- **Version labels** (CHK019): the route stays `/api/v1`. The OpenAPI `info.version` is the **document
  revision** (`1.1`), not the API version. Both contracts say so.
- **`traceId`** (FR-013): the change from `traceparent` to the 32-hex trace id is listed in the API contract's
  change notes.
- **Migration** (FR-006): the README shows the v1 body before and after, as JSON side by side.

**Test-harness addendum** (analyze U1, third run)

- `PhoneBook.Identity.IntegrationTests` references only the two `src` hosts, not `PhoneBook.Api.IntegrationTests`.
  It therefore gets **its own** `Infrastructure/LogCapture.cs`, an `ILogEventSink` of about 20 lines, instead of
  sharing the API test project's copy.
- A shared test-utilities project was considered and rejected, because it adds a project for two small classes.
- The Identity telemetry test adds the capture first, and the seeded-users test reuses it.
