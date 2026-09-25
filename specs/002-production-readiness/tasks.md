---

description: "Task list for Production Readiness (002-production-readiness)"
---

# Tasks: Production Readiness: Pagination, Rate Limiting, Observability, End-User Sign-In

**Input**: Design documents from `/specs/002-production-readiness/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Required. Constitution **v1.0.1** Principle V and the spec's delivery constraints say tests come first,
with integration tests on both database providers where behaviour depends on the database. The feature-001 split
still applies:
- domain unit tests only for domain rules (the domain does not change, so there are no new unit tests)
- integration tests for queries, endpoints, rate limiting, telemetry and sign-in
- architecture tests for layer rules

In every story the test tasks come **before** the implementation tasks: write them, see them fail, then implement.

**Revision 2 (2026-09-25)**: This revision is regenerated after the first `/speckit-analyze` run, constitution
**v1.0.2**, plan revision 2 and the FR-012 clarification. Tasks are renumbered; nothing had been implemented.
Changes by finding:
- **C1 / X1**: resolved in the constitution.
- **T1**: rate-limit hosts are created fresh per test (research R-07).
- **T2**: PostgreSQL telemetry tests run in the `Postgres` collection on the shared host.
- **U1**: `IdentityFactory` is unsealed, with an overridable limit.
- **U2**: US1 starts with compile-first stubs.
- **G1**: cross-service trace tests (FR-012 as clarified).
- **G2**: a trace for `429`.
- **A1**: SC-005 is automated as "no blocking" and measured manually.
- **U3**: empty and overflowing paging values.
- **E1 / I2**: documentation.
- **D1**: `errorCode` is set in one place.
- **New, M1**: per-host metrics through `IMeterFactory`, so metric assertions in parallel test hosts are exact (research R-07).

**Revision 5 (2026-09-25)**: This revision aligns the tasks with plan revision 6 (`/speckit-clarify`: an English-only sign-in page). T059 and T069 are updated. There is no renumbering and no new task.

**Revision 4 (2026-09-25)**: This revision aligns the tasks with plan revision 5 (third `/speckit-analyze` run).
Tasks are not renumbered and none are added:
- **U1**: T044 creates the Identity test project's own `LogCapture`, and T061 reuses it.
- **C1**: the session lifetime is configurable (T064, T067), and T058 tests a session that expires mid-sign-in.

**Revision 3 (2026-09-25)**: This revision covers the spec refinement from the security and API checklist
(FR-004 to FR-027; the new FR-024 to FR-027) and plan revisions 3 and 4 (research R-07 and R-08). It also folds in the
second analysis run's findings: P1 (capture filter), U1 (parent span), I2, A1 and A2 (pinned versions), F1 and C1.
Tasks are renumbered; nothing had been implemented.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md), so each story can be built, tested and
shipped on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = Paging, US2 = Rate limiting, US3 = Observability, US4 = End-user sign-in
- Paths are relative to the repository root (`D:\Programming\Projects\Interview Tasks\PhoneBook`)

## Global conventions (apply to every task)

- **Feature-001 conventions still apply**: see the "Global conventions" section of
  [`../001-phonebook-management/tasks.md`](../001-phonebook-management/tasks.md). In short:
  - `net10.0`, warnings as errors
  - sealed types, file-scoped namespaces, one public type per file
  - xUnit v3 and Shouldly, with tests named `Method_Scenario_ExpectedResult`
  - web hosts are referenced from tests through `ApiAssemblyMarker` and `IdentityAssemblyMarker`
  - MediatR stays pinned at 12.5.0
- **No new `try/catch` sites** (Principle II). The only allowed sites are `UnhandledExceptionBehavior`,
  `GlobalExceptionHandler`, `WriteDbContext` and `DomainEventDispatcher`. Telemetry export failures are handled
  inside the OpenTelemetry exporter. Login failures return a result, never an exception.
- Every new API error response (`400` paging validation, `429`) is ProblemDetails with `errorCode` and `traceId`
  (Principle III). Identity OAuth and login endpoints use OAuth or HTML responses, which v1.0.1 exempts.
- **Portable SQL only** (Principle IV). Paging uses `LIMIT @Take OFFSET @Skip` and a plain `ORDER BY`, with no
  `COLLATE` clause. Identical ordering comes from database collation (research R-02).
- **Never put personal data in telemetry or logs** (FR-016): no names, phone numbers, tag values, user names,
  passwords, client secrets or tokens.
- **Plain-text credentials** go only in `appsettings.Development.json` and carry a `DEV-ONLY` marker (Principle VI).
- **Configuration that tests override** (`WebApplicationFactory.UseSetting`) is read at runtime through options,
  never while registering services. This is the lesson from feature 001.
- **Tests first, compiling** (research R-07):
  - When a test needs a new public type or signature, a stub task adds it first, keeping the old behaviour.
  - The new test then compiles and fails on an assertion, not a build error.
- **Test isolation** (research R-07):
  - Rate-limit tests create their own host for each test.
  - PostgreSQL tests (including telemetry) run only in `[Collection("Postgres")]`.
  - Telemetry tests filter captured spans by the `TraceId` of their own requests.
  - Metric tests measure through `MetricCollector<T>`, bound to the host's own `IMeterFactory`.
- **Checkpoint rule**: at the end of every phase, run
  `dotnet build PhoneBook.slnx -c Release` (0 warnings) and `dotnet test --solution PhoneBook.slnx -c Release`
  (all green, Docker running).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the new packages (research R-06) and confirm the baseline.

- [X] T001 Add these `PackageVersion` entries to `Directory.Packages.props`, in new `ItemGroup`s labelled "Telemetry" and "Identity users", each with a licence comment:
  - `OpenTelemetry.Extensions.Hosting` 1.19.1
  - `OpenTelemetry.Instrumentation.AspNetCore` 1.19.0
  - `OpenTelemetry.Instrumentation.Http` 1.19.0
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.19.1
  - `OpenTelemetry.Exporter.InMemory` 1.19.1 (tests only)
  - `Npgsql.OpenTelemetry` 10.0.3
  - `Microsoft.Extensions.Diagnostics.Abstractions` 10.0.12 (`IMeterFactory` for the Application layer; MIT)
  - `Microsoft.Extensions.Diagnostics.Testing` 10.10.0 (`MetricCollector<T>`, tests only; MIT; research R-06)
  - ~~`Microsoft.Extensions.Identity.Core` 10.0.12~~ *(implementation note: not needed. It is part of the ASP.NET Core shared framework, and an explicit reference fails the build with NU1510. `PasswordHasher` is used from the framework.)*
- [X] T002 [P] Add `PackageReference`s to `src/PhoneBook.Api/PhoneBook.Api.csproj`: the four OpenTelemetry host packages (Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.Http, Exporter.OpenTelemetryProtocol) and `Npgsql.OpenTelemetry`.
- [X] T003 [P] Add `PackageReference`s to `src/PhoneBook.Identity/PhoneBook.Identity.csproj`: the four OpenTelemetry host packages. `Microsoft.Extensions.Identity.Core` comes from the shared framework; see the T001 note.
- [X] T004 [P] Add a `PackageReference` to `Microsoft.Extensions.Diagnostics.Abstractions` in `src/PhoneBook.Application/PhoneBook.Application.csproj`, for `IMeterFactory`. It is BCL-level, not a provider driver, so the architecture tests allow it.
- [X] T005 [P] Add `PackageReference`s to `OpenTelemetry.Exporter.InMemory` and `Microsoft.Extensions.Diagnostics.Testing` in `tests/PhoneBook.Api.IntegrationTests/PhoneBook.Api.IntegrationTests.csproj` and in `tests/PhoneBook.Identity.IntegrationTests/PhoneBook.Identity.IntegrationTests.csproj`.
- [X] T006 Run the checkpoint commands to confirm the baseline: 0 warnings, and 132 of 132 tests passing with no code changes yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Changes that several stories depend on: byte-order collation for PostgreSQL, test factories that
cannot hit the new rate limits, and the shared telemetry names.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 In `tests/PhoneBook.Api.IntegrationTests/Infrastructure/PostgresContainerFixture.cs`, add `.WithEnvironment("POSTGRES_INITDB_ARGS", "--locale=C --encoding=UTF8")` to the `PostgreSqlBuilder`, so the test database compares text by bytes (research R-02). Explain why in an XML comment.
- [X] T008 Create `tests/PhoneBook.Api.IntegrationTests/Providers/PostgresCollationTests.cs` in the `Postgres` collection. It asserts that `SELECT datcollate FROM pg_database WHERE datname = current_database()` returns `C`. Do not use `SHOW lc_collate`, which PostgreSQL 16+ removed. This guards the deployment requirement. Run it after T007, since it only passes once the container is created with the C locale (F1).
- [X] T009 [P] In `docker-compose.yml`, add `POSTGRES_INITDB_ARGS: "--locale=C --encoding=UTF8"` to the `postgres` service environment, with a comment "deployment requirement: byte-order collation for paging (feature 002, research R-02)".
- [X] T010 [P] Make the shared test hosts effectively unlimited, so feature-001 load tests (for example 100 mixed requests) never hit the new limits (research R-03, R-07):
  - in `tests/PhoneBook.Api.IntegrationTests/Infrastructure/PhoneBookApiFactory.cs` and `.../SqliteApiFactory.cs`, add `builder.UseSetting("RateLimiting:Api:PermitLimit", "1000000")`
  - in `tests/PhoneBook.Identity.IntegrationTests/Infrastructure/IdentityFactory.cs`:
    - **unseal** the class (`public class IdentityFactory`)
    - add `protected virtual int TokenPermitLimit => 1_000_000;`
    - in `ConfigureWebHost`, call `builder.UseSetting("RateLimiting:Token:PermitLimit", TokenPermitLimit.ToString(CultureInfo.InvariantCulture))`
  - do the same (`RateLimiting:Api:PermitLimit`) for the API factory built inside `tests/PhoneBook.Identity.IntegrationTests/EndToEndTokenToApiTests.cs`
- [X] T011 [P] Create two files in `src/PhoneBook.Application/Abstractions/Telemetry/`, using only BCL `System.Diagnostics` and `System.Diagnostics.Metrics` plus `IMeterFactory` (data-model §3):
  - **`PhoneBookTelemetry.cs`**: a public static class with:
    - `ActivitySource Application` named `PhoneBook.Application`, and `ActivitySource Persistence` named `PhoneBook.Persistence`. These are static, because spans are filtered by trace id.
    - `const string` names for the tags `phonebook.request`, `phonebook.result`, `db.system`, `db.operation` and `policy`, and for the meter name `PhoneBook`.
  - **`PhoneBookMetrics.cs`**: a public sealed class whose constructor takes `IMeterFactory` and calls `meterFactory.Create("PhoneBook")`. It exposes `Counter<long>` members `ContactsCreated` (`phonebook.contacts.created`), `ContactsUpdated` (`phonebook.contacts.updated`), `ContactsDeleted` (`phonebook.contacts.deleted`) and `RateLimitRejections` (`phonebook.ratelimit.rejections`).

  Register `PhoneBookMetrics` as a singleton in `src/PhoneBook.Application/DependencyInjection.cs`. Because the meter comes from the host's `IMeterFactory`, each test host's metrics can be measured on their own (M1, research R-07); a static `Meter` would mix counts from parallel test hosts.
- [X] T012 [P] Create `src/PhoneBook.Identity/Telemetry/IdentityMetrics.cs`: a sealed class with an `IMeterFactory` constructor that creates meter `PhoneBook` and `Counter<long> RateLimitRejections` (`phonebook.ratelimit.rejections`, tag `policy`). Register it as a singleton in `src/PhoneBook.Identity/Program.cs`. The Identity host does not reference Application, so it has its own class with the same meter name.
- [X] T013 Checkpoint. The build has 0 warnings, and all feature-001 tests plus T008 pass.

**Checkpoint**: The foundation is ready. User stories can now start, in parallel if staffed.

---

## Phase 3: User Story 1 - Page through large tag results (Priority: P1) 🎯 MVP

**Goal**: `GET /api/v1/contacts?tag=&page=&pageSize=` returns `{ items, page, pageSize, totalCount, hasNext }`,
sorted in SQL by `last_name, first_name, id` with byte-order collation on both providers (FR-001 to FR-006,
research R-01 and R-02). This is a deliberate breaking change to v1.

**Independent Test**: Seed 250 contacts under one tag. Page 1 with a page size of 100 returns 100 items,
`totalCount` 250 and `hasNext` true. Page 3 returns 50 items and `hasNext` false. The same results come back on
SQLite and on PostgreSQL.

### Compile-first stubs for User Story 1 (research R-07)

- [X] T014 [P] [US1] Create `src/PhoneBook.Application/Abstractions/Paging/PagingDefaults.cs`: a static class with `DefaultPage = 1`, `DefaultPageSize = 50` and `MaxPageSize = 200` (data-model §1), and the error codes `PageInvalid = "Paging.Page.Invalid"` and `PageSizeInvalid = "Paging.PageSize.Invalid"`.
- [X] T015 [P] [US1] Create `src/PhoneBook.Application/Abstractions/Paging/PagedResponse.cs`: a public `sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)` with a computed `bool HasNext => (long)Page * PageSize < TotalCount`, which uses `long` so a huge page number cannot overflow.
- [X] T016 [US1] **Compile-first stub** (research R-07):
  - Change `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQuery.cs` to `GetContactsByTagQuery(string Tag, int Page = PagingDefaults.DefaultPage, int PageSize = PagingDefaults.DefaultPageSize) : IQuery<PagedResponse<ContactResponse>>`.
  - Minimally adapt `GetContactsByTagQueryHandler.cs`: keep the current SQL and in-memory sort, and wrap **all** rows as `new PagedResponse<ContactResponse>(rows, query.Page, query.PageSize, rows.Count)`.
  - In `src/PhoneBook.Api/Endpoints/Contacts/GetContactsByTag.cs`, send the extended query with defaults and change `.Produces<>` to `PagedResponse<ContactResponse>`.
  - The solution builds, and the paging tests written next fail on behaviour.

### Tests for User Story 1 ⚠️ (write first; they must fail on assertions)

- [X] T017 [P] [US1] Create `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactsByTagPagingTests.cs`. Follow the pattern of the performance tests: an abstract `GetContactsByTagPagingTestsBase(IPhoneBookApiFactory factory)`, with sealed `PostgresGetContactsByTagPagingTests` in `[Collection(IntegrationTestCollection.Name)]` and `SqliteGetContactsByTagPagingTests` in `[Collection(SqliteIntegrationTestCollection.Name)]`. Scenarios, all through HTTP with the paged JSON shape:
  - **AC1**: 250 contacts with the tag `همکار`, page 1 with page size 100 → 100 items in order, `totalCount` 250, `page` 1, `pageSize` 100, `hasNext` true
  - **AC2**: page 3 → 50 items, `hasNext` false
  - **AC3**: no paging parameters → 50 items (the default), `page` 1, `pageSize` 50
  - **AC4**: 30 contacts, page 5 with page size 20 → `200`, empty `items`, `totalCount` 30, `hasNext` false
  - **AC6**: an unknown tag → empty `items`, `totalCount` 0, `hasNext` false
  - **SC-002 walk**: fetch every page of 250 at page size 40. Each id appears exactly once, and the concatenated order equals the expected list sorted with `StringComparer.Ordinal` by last name, then first name. Use Persian and Latin names, including names that differ only in `ی` (U+06CC) and `ي` (U+064A).
  - **Tie-break**: 5 contacts with the same first and last name and different phone numbers → ordered by `id` ascending, with the same order on both providers
  - **Large page number**: `page=1000000` with `pageSize=200` → `200` with an empty page. This proves the offset is computed without `int` overflow.
- [X] T018 [P] [US1] Add validation tests to `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/GetContactsByTagEndpointTests.cs`:
  - `page=0` → `400` with an `errors.page` entry carrying `Paging.Page.Invalid`
  - `pageSize=0` and `pageSize=201` → `400` with `errors.pageSize` carrying `Paging.PageSize.Invalid`
  - `page=abc`, `page=-1`, `page=99999999999` (overflow) and `pageSize=x` → `400` ProblemDetails with the same per-field codes, never a framework binding error (data-model §1)
  - `page=` and `pageSize=` (empty) → `200` using the defaults (`page` 1, `pageSize` 50)
  - `tag=` (blank) with `page=0` → `400` whose errors hold **both** `tag` (`Tag.Required`) and `page`, in one response
  - each `400` has `errorCode` `General.Validation` and a `traceId`
- [X] T019 [US1] Move the existing tag-search tests to the paged shape, after T016, without changing their expected contacts or order (SC-008):
  - in `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/GetContactsByTagEndpointTests.cs`, deserialize into a local `PagedContacts` record (`Items`, `Page`, `PageSize`, `TotalCount`, `HasNext`) and assert on `Items`
  - in `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactsByTagQueryTests.cs`, use `result.Value.Items` and `result.Value.TotalCount`
  - add a comment that references FR-006 as the reason
- [X] T020 [US1] Extend `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactsByTagPerformanceTests.cs` for SC-001, keeping the existing bulk-insert helper and the per-provider subclasses:
  - rename the test to `SearchByTag_PageOf100000TaggedContacts_ReturnsUnder500Ms`
  - insert 100,000 rows with the tag `perf`, plus 10,000 rows with other tags, in batches of 5,000 per transaction
  - after one warm-up call, assert that page 1 **and** the last page (page 2000 at page size 50), each measured separately, return in under 500 ms with `TotalCount` 100,000
  - keep `[Trait("Category", "Performance")]`
- [X] T021 [P] [US1] Create `tests/PhoneBook.Api.IntegrationTests/Providers/SchemaIndexTests.cs` with a subclass per provider. It asserts that the index `ix_contacts_tag_order` exists and `ix_contacts_normalized_tag` does not:
  - SQLite: `SELECT name FROM sqlite_master WHERE type='index'`
  - PostgreSQL: `SELECT indexname FROM pg_indexes WHERE tablename='contacts'`

### Implementation for User Story 1

- [X] T022 [US1] Extend `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQueryValidator.cs` with two rules, both collected into the same `ValidationError` as the existing tag rules (feature 001, FR-012):
  - `Page`: "≥ 1; default 1", `.WithName("page")`, `.WithErrorCode(PagingDefaults.PageInvalid)`, message "Page must be 1 or greater."
  - `PageSize`: "1–200; default 50", `.WithName("pageSize")`, `.WithErrorCode(PagingDefaults.PageSizeInvalid)`, message "Page size must be between 1 and 200."
- [X] T023 [US1] Finish `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQueryHandler.cs`, replacing the T016 stub body (research R-02):
  - Use two parameterised statements on one connection:
    - `SELECT COUNT(*) FROM contacts WHERE normalized_tag = @NormalizedTag`
    - the existing column list plus `ORDER BY last_name, first_name, id LIMIT @Take OFFSET @Skip`
  - Compute `Skip` as `(long)(Page - 1) * PageSize`.
  - Skip the page query when `TotalCount == 0` or `Skip >= TotalCount`.
  - Remove the in-memory `OrderBy(StringComparer.Ordinal)` sort.
  - Update the XML doc to explain that the ordering is byte order on both providers because of the collation (SQLite `BINARY`, PostgreSQL `LC_COLLATE=C`).
  - Read `COUNT(*)` as `long` (PostgreSQL returns `bigint`) and convert it with `checked((int)count)`.
- [X] T024 [US1] Update `src/PhoneBook.Infrastructure/Persistence/DatabaseInitializer.cs` `IndexStatements`, keeping `ux_contacts_phone_tag` unchanged:
  - add `DROP INDEX IF EXISTS ix_contacts_normalized_tag`
  - replace the old index with `CREATE INDEX IF NOT EXISTS ix_contacts_tag_order ON contacts (normalized_tag, last_name, first_name, id)`
  - update the class comment to mention the paging index
- [X] T025 [US1] Finish `src/PhoneBook.Api/Endpoints/Contacts/GetContactsByTag.cs` (data-model §1, "Parsing query values"):
  - bind the query values `page` and `pageSize` as `string?`
  - a missing, empty or whitespace value uses `PagingDefaults`
  - otherwise parse with `int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)`. A failure (non-numeric, a sign or overflow) passes `0`, so the validator reports the field's code (T018) and the error contract stays consistent.
  - `.ProducesProblem(StatusCodes.Status400BadRequest)` and `.ProducesProblem(StatusCodes.Status429TooManyRequests)`
  - an XML summary that says "BREAKING (FR-006)"
- [X] T026 [US1] Checkpoint. All US1 tests pass on both providers, the rest of the suite stays green, and the build has 0 warnings.

**Checkpoint**: US1 works and can be shipped on its own (the MVP).

---

## Phase 4: User Story 2 - Protect the service from excessive use (Priority: P1)

**Goal**: A fixed-window rate limiter partitioned per caller:
- **API policy `api`**: 100 per 60 s, keyed by the `sub` claim, otherwise the IP address
- **Identity policy `token`**: 10 per 60 s per IP, on `POST /connect/token` (and on login in US4)

Rejected requests get `429` with `Retry-After`. Health checks are never limited (FR-007 to FR-011, research R-03).

**Independent Test**: With `RateLimiting:Api:PermitLimit=5`, a client's 6th request gets `429` with
`RateLimit.Exceeded` and `Retry-After`, while a second client still gets `200`.

### Tests for User Story 2 ⚠️ (write first; they must fail)

- [X] T027 [US2] Extend `tests/PhoneBook.Api.IntegrationTests/Infrastructure/TestAuthHandler.cs` with an optional `X-Test-Sub` header (constant `SubHeader`, default `test-client`), which becomes the `sub` claim so tests can act as different callers. Add `WithSub(this HttpClient, string sub)` to `.../TestAuthExtensions.cs`. Existing tests stay unchanged.
- [X] T028 [P] [US2] Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/RateLimitedApiFactory.cs` (research R-07):
  - An SQLite factory with its own in-memory database name, `RateLimiting:Api:PermitLimit=5`, `RateLimiting:Api:WindowSeconds=2` and test authentication.
  - It implements `IPhoneBookApiFactory`, including the `Capture` from T038.
  - It is **not** a fixture. Each test class creates it in its constructor and disposes it in `DisposeAsync`, so every test starts with fresh counters (analyze T1).
  - *Implementation note:* the window defaults to 60 s through a constructor parameter, because a fixed window resets on its own timer and a 2 s window could reset mid-burst and make tests flaky. Only the Retry-After test uses `new RateLimitedApiFactory(windowSeconds: 2)`.
- [X] T029 [P] [US2] Create `tests/PhoneBook.Api.IntegrationTests/RateLimiting/ApiRateLimitTests.cs`, using a fresh `RateLimitedApiFactory` per test and a distinct `sub` per test (`$"{testName}-{Guid.NewGuid():N}"`):
  - **AC1**: 5 × `GET /api/v1/contacts?tag=x` → `200`. The 6th → `429`, `Content-Type: application/problem+json`, `status` 429, `errorCode` `RateLimit.Exceeded`, a non-empty `traceId`, and an integer `Retry-After` header ≥ 1.
  - **AC2 / SC-003**: sub A sends 50 requests, 10 times its allowance, and all 45 excess requests get `429`. Interleaved, sub B sends 5 requests, and all get `200` (0% rejected).
  - **AC3**: after a `429`, wait for `Retry-After` plus 200 ms, and the next request gets `200`.
  - **Anonymous callers are partitioned by IP**: 6 requests with `X-Test-Scopes: none` → the first 5 get `401` and the 6th gets `429`.
  - **429 takes precedence** (FR-007): with `X-Test-Scopes: phonebook.read`, 6 × `POST` → the first 5 get `403` and the 6th gets `429`, not `403`.
  - **Only `Retry-After`** (FR-009): no response header starts with `RateLimit`.
  - **AC5 / FR-010**: 50 × `GET /health/ready`, `/health/live` and `/swagger/v1/swagger.json` → never `429`.
  - **Write routes are limited too**: `POST` counts against the same allowance.
- [X] T030 [P] [US2] Create `tests/PhoneBook.Identity.IntegrationTests/Infrastructure/RateLimitedIdentityFactory.cs` (`: IdentityFactory`, `protected override int TokenPermitLimit => 3`; relies on T010 unsealing the base) and `tests/PhoneBook.Identity.IntegrationTests/TokenRateLimitTests.cs`. Each test creates its own `RateLimitedIdentityFactory` in the constructor and disposes it in `DisposeAsync`: every in-process request shares the partition `ip:unknown`, so a shared host would leak counts (research R-07).
  - **AC4**: 3 valid client-credentials requests → `200`. The 4th → `429`, with the header `Retry-After` and the JSON body `{"error":"temporarily_unavailable","error_description":"Too many requests. Retry later."}`.
  - **Brute-force protection** (constitution v1.0.2, VI): requests with a **wrong client secret** also count. After 3 × `invalid_client`, the 4th gets `429`. This proves the limiter runs before OpenIddict handles the request (T035).
  - **FR-010**: `/health/ready`, `/.well-known/openid-configuration` and the JWKS URI (taken from discovery) are never limited, 20 calls each.

### Implementation for User Story 2

- [X] T031 [P] [US2] Create `src/PhoneBook.Api/Infrastructure/RateLimiting/RateLimitOptions.cs` for section `RateLimiting`:
  - a nested `FixedWindowPolicyOptions Api` with `PermitLimit = 100` and `WindowSeconds = 60`, both `[Range(1, int.MaxValue)]`
  - bound with `AddOptions<RateLimitOptions>().BindConfiguration("RateLimiting").ValidateDataAnnotations().ValidateOnStart()`
  - include the policy name constant `ApiPolicy = "api"`
- [X] T032 [US2] Create `src/PhoneBook.Api/Infrastructure/RateLimiting/RateLimitingSetup.cs` with `AddPhoneBookRateLimiting(this IServiceCollection)`:
  - **Policy `api`** via `AddPolicy(ApiPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(key, …))`:
    - the partition key is `"sub:" + sub` for an authenticated user, otherwise `"ip:" + RemoteIpAddress`, or `"ip:unknown"` when there is none
    - the options come from `IOptionsMonitor<RateLimitOptions>` resolved from `httpContext.RequestServices` **at request time**: `PermitLimit`, `Window = TimeSpan.FromSeconds(WindowSeconds)`, `QueueLimit = 0`, `AutoReplenishment = true`
  - `RejectionStatusCode = 429`.
  - **`OnRejected`**:
    - write `Retry-After` from `MetadataName.RetryAfter` (seconds, rounded up, at least 1)
    - increment `PhoneBookMetrics.RateLimitRejections` (resolved from `RequestServices`) with the tag `policy=api`
    - write ProblemDetails through `IProblemDetailsService` with status 429, title "Too many requests" and type `https://tools.ietf.org/html/rfc6585#section-4`. Do **not** set `errorCode` here: T033's status switch adds `RateLimit.Exceeded` and `traceId` in one place (analyze D1).
    - no try/catch
- [X] T033 [US2] Update `src/PhoneBook.Api/Program.cs`:
  - call `builder.Services.AddPhoneBookRateLimiting()`
  - add `StatusCodes.Status429TooManyRequests => "RateLimit.Exceeded"` to the `errorCode` switch in `CustomizeProblemDetails`
  - order the middleware as `app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization();`, so `sub` is known when the partition is chosen
  - call `.RequireRateLimiting(RateLimitOptions.ApiPolicy)` on the `api` route group
  - call `.DisableRateLimiting()` on both `MapHealthChecks` calls
  - add a comment that references research R-03
- [X] T034 [P] [US2] Add `"RateLimiting": { "Api": { "PermitLimit": 100, "WindowSeconds": 60 } }` to `src/PhoneBook.Api/appsettings.json`.
- [X] T035 [US2] Create `src/PhoneBook.Identity/RateLimiting/TokenRateLimitOptions.cs` (section `RateLimiting:Token`, `PermitLimit = 10`, `WindowSeconds = 60`, `[Range(1, int.MaxValue)]`, `ValidateOnStart`, constant `TokenPolicy = "token"`) and `src/PhoneBook.Identity/RateLimiting/RateLimitingSetup.cs`:
  - **Policy `token`**: partitioned by `"ip:" + RemoteIpAddress`, fixed window, `QueueLimit = 0`, with options read from `IOptionsMonitor` at request time.
  - **`OnRejected`**:
    - `429`
    - `Retry-After`
    - `IdentityMetrics.RateLimitRejections` with `policy=token`
    - the body `{"error":"temporarily_unavailable","error_description":"Too many requests. Retry later."}` with `application/json`, a documented extension (research R-08)
    - US4 adds an HTML branch for `POST /account/login` (T070)
  - **Pipeline in `src/PhoneBook.Identity/Program.cs`** (research R-03): `app.UseRouting(); app.UseCors(...); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();`.
    - The limiter **must run before `UseAuthentication`**, because OpenIddict handles and answers `/connect/token` requests (including `invalid_client`) inside the authentication middleware.
    - `UseRouting` first makes the endpoint's rate-limit metadata visible.
    - CORS preflight is answered before the limiter.
  - **Endpoints**:
    - `.RequireRateLimiting(TokenPolicy)` on the endpoint returned by `MapTokenEndpoint` (change `src/PhoneBook.Identity/Endpoints/TokenEndpoint.cs` to return the `RouteHandlerBuilder`)
    - `.DisableRateLimiting()` on the health checks
- [X] T036 [P] [US2] Add `"RateLimiting": { "Token": { "PermitLimit": 10, "WindowSeconds": 60 } }` to `src/PhoneBook.Identity/appsettings.json`.
- [X] T037 [US2] Checkpoint. All US2 tests pass, the feature-001 load and concurrency tests stay green (thanks to T010), and the build has 0 warnings.

**Checkpoint**: US1 and US2 both work on their own.

---

## Phase 5: User Story 3 - Operators can observe the running service (Priority: P2)

**Goal**: OpenTelemetry traces and metrics in both hosts:
- **Spans**: HTTP server and client, `PhoneBook.Application`, `PhoneBook.Persistence`, and Npgsql
- **Metrics**: the built-in HTTP and rate-limiting meters, plus the `PhoneBook` meter
- **Export**: OTLP when `Telemetry:OtlpEndpoint` is set
- **Error `traceId`**: the W3C trace id
- **No personal data** anywhere

This covers FR-012 to FR-017 and research R-04.

**Independent Test**: Create a contact with the in-memory exporter attached. One trace holds the HTTP server
span, the command span and the persistence span. A `404`'s `traceId` equals its trace's id, and the
`phonebook.contacts.created` counter increases by 1.

### Tests for User Story 3 ⚠️ (write first; they must fail)

- [X] T038 [US3] Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/TelemetryCapture.cs` (research R-07):
  - It holds thread-safe collections of `Activity` and `Metric`.
  - `AddTo(IServiceCollection)` calls `services.ConfigureOpenTelemetryTracerProvider(b => b.AddInMemoryExporter(...))` and `ConfigureOpenTelemetryMeterProvider(b => b.AddInMemoryExporter(...))`.
  - `ForTrace(ActivityTraceId)` returns the spans of one trace.
  - `FlushAsync(IServiceProvider)` calls `ForceFlush` on the providers.
  - `AllRecordedText()` gathers every activity's display name, tags, events, status description and baggage, plus every metric's name and point tags, for the personal-data scan.

  - **Filtering processor** (research R-07, analyze P1): keep an activity only when it is an ASP.NET Core server span, has a parent, or comes from `PhoneBook.Application` or `PhoneBook.Persistence`. Other root spans, such as per-row Npgsql inserts from test seeding, are dropped before they are stored.
  - **Log capture** (research R-08): `LogCapture : ILogEventSink` holds rendered messages and property values. It is registered as a singleton `ILogEventSink` in DI, where Serilog's `ReadFrom.Services` picks it up. `AllRecordedText()` includes it.

  Add `TelemetryCapture Capture { get; }` to `.../IPhoneBookApiFactory.cs`, if T028 has not already added it (I2).
  *Implementation note (found in US4):* both hosts now call `UseSerilog(..., preserveStaticLogger: true)`, and request logging uses the host's own `Serilog.ILogger`. With the default, every host replaces the global `Log.Logger`, so parallel test hosts wrote into each other's sinks, and log scans could pass without checking anything. Attach a capture in `PhoneBookApiFactory` and `SqliteApiFactory` through `ConfigureTestServices`; this is always on and harmless. **No separate PostgreSQL telemetry factory**: PostgreSQL telemetry tests use the `Postgres` collection's shared host, so no second host resets the database while tests run (analyze T2). Tests read only the spans of their own requests (`ForTrace`), because `ActivitySource`s are process-wide.
- [X] T039 [P] [US3] Create `tests/PhoneBook.Api.IntegrationTests/Telemetry/TracingTests.cs`: an abstract base, with sealed subclasses in `[Collection(IntegrationTestCollection.Name)]` and `[Collection(SqliteIntegrationTestCollection.Name)]`. Each test reads its trace id from the response `traceId` or from a `traceparent` header it sends, then asserts on `Capture.ForTrace(id)`:
  - **AC1**: `POST /api/v1/contacts` → spans in one trace:
    - a server span with the templated `http.route`
    - a `PhoneBook.Application` span `CreateContactCommand` with `phonebook.result=success`, whose parent chain reaches the server span
    - a `PhoneBook.Persistence` span with `db.operation=save` and `db.system` equal to `sqlite` or `postgresql`
    - on PostgreSQL, also at least one Npgsql span
  - **AC2 / SC-004**: `404` (random id), `400` (blank tag) and `409` (duplicate contact) responses each have a `traceId` of 32 lowercase hexadecimal characters that equals their server span's `TraceId`.
  - **Incoming propagation**: a request carrying a W3C `traceparent` produces spans with that trace id.
  - A failed command's span has `phonebook.result` equal to the error code and status `Error`.
  - A tag search produces a `PhoneBook.Persistence` span `query.contacts_by_tag`.
- [X] T040 [P] [US3] Create `tests/PhoneBook.Api.IntegrationTests/Telemetry/MetricsTests.cs` (SQLite collection). Measure with `new MetricCollector<long>(factory.Services.GetRequiredService<IMeterFactory>(), "PhoneBook", "<instrument>")`, created at the start of each test; it sees only this host's meters (M1).
  - **AC3**: after one create, one update and one delete, `phonebook.contacts.created`, `.updated` and `.deleted` each record exactly 1.
  - `http.server.request.duration`, collected through a `MetricCollector<double>` on meter `Microsoft.AspNetCore.Hosting`, has a measurement tagged with `http.route`, `http.response.status_code` and `http.request.method`.
  - **Rate limiting** (with a fresh `RateLimitedApiFactory`):
    - `phonebook.ratelimit.rejections` with `policy=api` records 1 per `429`
    - **edge case, G2**: the `429` response's `traceId` has a server span in `Capture.ForTrace(...)` with `http.response.status_code=429`
- [X] T041 [P] [US3] Create `tests/PhoneBook.Api.IntegrationTests/Telemetry/PersonalDataTelemetryTests.cs` (FR-016, SC-006, AC5): an abstract base, with sealed subclasses in the `Postgres` and `Sqlite` collections.
  1. Use distinctive values: first name `Zyxwvfirst`, last name `Qponmlast`, phone `09129998877`, tag `tag-qqzz-secret`, and a Persian name `ژاله‌تست`.
  2. Run create, get by id, search by tag (with page parameters), update, a validation failure echoing the values, a duplicate conflict and delete.
  3. Flush, then assert that no value from step 1 appears in `Capture.AllRecordedText()` (spans, metrics **and logs**), case-insensitively. This deliberately scans **everything** the host captured, not only this test's traces.
  4. Assert that no span has a `url.query` tag containing `tag=`, and that no span has a `client.address` tag.
     *Implementation note:* the literal texts `127.0.0.1` and `::1` are not scanned. Npgsql spans legitimately carry the database server's address (`server.address`), which is infrastructure data, while FR-016 forbids *client* addresses, and those are covered by the `client.address` check.
- [X] T042 [P] [US3] Create `tests/PhoneBook.Api.IntegrationTests/Telemetry/TelemetryResilienceTests.cs` (AC4, FR-017; research R-04):
  - **Unreachable exporter**: a fresh SQLite factory whose `ConfigureTestServices` adds `AddOtlpExporter(o => o.Endpoint = new Uri("http://127.0.0.1:9"))` to both providers → 50 mixed requests all return their normal status, and no single request takes 1 s or more. This proves export never blocks.
  - **Default configuration** (empty `Telemetry:OtlpEndpoint`) → requests succeed.
  - The 5% comparison in SC-005 is measured manually (quickstart #13, T080). No latency ratio is asserted in CI (analyze A1).
  - *Implementation note:* one untimed warm-up request, then a 2 s per-request bound. The 1 s bound flaked once under full-suite load, and a blocking export would take about 10 s (the connect timeout).
- [X] T043 [P] [US3] Create `tests/PhoneBook.Api.IntegrationTests/Telemetry/OutgoingCallTracingTests.cs`, for the phone book service's half of FR-012 as clarified (research R-04):
  - Start a parent span with `using var parent = PhoneBookTelemetry.Application.StartActivity("test.outgoing")`. This source is already recorded by the host's tracer (U1).
  - Inside it, create a client from the SQLite host's `IHttpClientFactory`, using the default socket primary handler so .NET's HTTP diagnostics run, and `GET http://127.0.0.1:9/.well-known/openid-configuration`. The connection is refused.
  - Assert that `Capture.ForTrace(parent.TraceId)` holds an HTTP client span whose `ParentSpanId` equals `parent.SpanId`, with `http.request.method=GET`, a `server.address`, and **no** `url.query`.
  - In-process `TestServer` handlers bypass .NET's HTTP diagnostics, so a real socket handler is required here.
- [X] T044 [P] [US3] Create `tests/PhoneBook.Identity.IntegrationTests/TelemetryTests.cs`. Attach an in-memory exporter to a fresh `IdentityFactory` subclass via `ConfigureTestServices`.
  - A client-credentials token request produces a server span for `/connect/token`.
  - **FR-016**: create `tests/PhoneBook.Identity.IntegrationTests/Infrastructure/LogCapture.cs`, this project's own `ILogEventSink` holding rendered messages and property values. This project does not reference `PhoneBook.Api.IntegrationTests` (research R-07, analyze U1). Register it as a singleton `ILogEventSink` in the Identity factory via `ConfigureTestServices`. After a token request and a sign-in (wrong and right password), no span, metric or log contains the client secret, `client_secret=`, the `access_token` value, `alice`, `alice-dev-password` or `127.0.0.1`.
  - **FR-012, the identity side**: `GET /.well-known/openid-configuration` sent with a W3C `traceparent` header produces a server span that continues that trace id. Together with T043, this proves one trace across both services. The live end-to-end view is quickstart #11.
  - `phonebook.ratelimit.rejections` with `policy=token` records 1 after a `429`. Use a fresh `RateLimitedIdentityFactory` and a `MetricCollector<long>` bound to its `IMeterFactory`.
- [X] T045 [P] [US3] Extend `tests/PhoneBook.ArchitectureTests/LayerDependencyTests.cs`:
  - add `"OpenTelemetry"` to the forbidden list for Domain, SharedKernel and Application, since Application may use BCL `System.Diagnostics` only (plan, Structure Decision)
  - add a test that `PhoneBookTelemetry` lives in `PhoneBook.Application.Abstractions.Telemetry`

### Implementation for User Story 3

- [X] T046 [US3] Update `src/PhoneBook.Application/Abstractions/Behaviors/LoggingBehavior.cs`:
  - wrap `next` in `using var activity = PhoneBookTelemetry.Application.StartActivity(requestName)`
  - set `phonebook.request` to the request type name
  - after the call, set `phonebook.result` to `success` or `response.Error.Code`, and on failure call `activity.SetStatus(ActivityStatusCode.Error, response.Error.Code)`
  - never tag request field values, and add no try/catch
- [X] T047 [US3] Add `string ProviderName { get; }` to `src/PhoneBook.Application/Abstractions/Data/ISqlConnectionFactory.cs`, returning `"sqlite"` or `"postgresql"`, and implement it in `src/PhoneBook.Infrastructure/Persistence/SqlConnectionFactory.cs` from `DatabaseOptions.Provider`. It supplies `db.system` without a driver reference in Application.
- [X] T048 [US3] In `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQueryHandler.cs`, wrap the COUNT and page queries in `PhoneBookTelemetry.Persistence.StartActivity("query.contacts_by_tag", ActivityKind.Client)` with the tags `db.system` (from `ProviderName`) and `db.operation=query.contacts_by_tag`. Record no parameter values.
- [X] T049 [US3] In `src/PhoneBook.Infrastructure/Persistence/WriteDbContext.cs`, start a `PhoneBookTelemetry.Persistence` activity `save` (`ActivityKind.Client`) in `IUnitOfWork.SaveChangesAsync`:
  - tag `db.system` from `Database.IsNpgsql()` / `IsSqlite()`, and `db.operation=save`
  - when the method returns a failure `Result` (the existing catch-and-translate path), set status `Error` with the error code
  - do not add a new catch
- [X] T050 [P] [US3] Inject `PhoneBookMetrics` into the existing domain-event handlers and increment the counters:
  - `ContactsCreated.Add(1)` in `src/PhoneBook.Application/Contacts/EventHandlers/ContactCreatedAuditHandler.cs`
  - `ContactsUpdated.Add(1)` in `.../ContactUpdatedAuditHandler.cs`
  - `ContactsDeleted.Add(1)` in `.../ContactDeletedAuditHandler.cs`
  - `ContactTagChangedAuditHandler` gets no counter: a tag change is also an update
  - *Implementation note (FR-016):* the created, deleted and tag-changed audit log messages no longer include tag values, only the contact id. The personal-data scan (T041) covers logs.
- [X] T051 [P] [US3] Create `src/PhoneBook.Api/Infrastructure/TraceIds.cs` with `public static string Current(HttpContext context) => Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier`. Use it:
  - in `Program.cs` `CustomizeProblemDetails` (replacing `Activity.Current?.Id`). *Implementation note:* this assigns `extensions["traceId"]` rather than calling `TryAdd`, because ASP.NET Core pre-fills `traceId` with the full `traceparent`.
  - in `src/PhoneBook.Api/Infrastructure/GlobalExceptionHandler.cs` line 30

  Add an XML comment explaining FR-013 and research R-04.
- [X] T052 [US3] Create `src/PhoneBook.Api/Infrastructure/Telemetry/TelemetryOptions.cs` (section `Telemetry`, `string? OtlpEndpoint`) and `src/PhoneBook.Api/Infrastructure/Telemetry/TelemetrySetup.cs` with `AddPhoneBookTelemetry(this WebApplicationBuilder)`:
  - **Resource**: `AddService("phonebook-api", serviceVersion: assembly informational version)`.
  - **Tracing**:
    - `AddAspNetCoreInstrumentation`:
      - `Filter` excludes `/health` and `/swagger`
      - `EnrichWithHttpRequest` calls `activity.SetTag("url.query", null)` and `activity.SetTag("client.address", null)` (FR-016)
      - `RecordException = false`, because exception messages may echo input
    - `AddHttpClientInstrumentation`, with the same `url.query` scrubbing through `EnrichWithHttpRequestMessage`
    - `AddNpgsql()`
    - `AddSource("PhoneBook.Application", "PhoneBook.Persistence")`
  - **Metrics**:
    - `AddAspNetCoreInstrumentation()`
    - `AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.RateLimiting", "PhoneBook")`
  - **Export**: register `UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(endpoint))` **only** when `builder.Configuration["Telemetry:OtlpEndpoint"]` is non-empty (FR-015). Tests attach exporters through `ConfigureTestServices` instead of overriding this key, so reading configuration at registration is acceptable here. Record that exception in a comment.
- [X] T053 [US3] Call `builder.AddPhoneBookTelemetry()` in `src/PhoneBook.Api/Program.cs`, and add `"Telemetry": { "OtlpEndpoint": "" }` to `src/PhoneBook.Api/appsettings.json`.
- [X] T054 [P] [US3] Create `src/PhoneBook.Identity/Telemetry/TelemetrySetup.cs`, mirroring T052 without Npgsql and application sources:
  - service name `phonebook-identity`
  - `url.query` and `client.address` scrubbed
  - `/health` filtered out
  - meters `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.RateLimiting` and `PhoneBook`
  - the same conditional OTLP export

  Call it from `src/PhoneBook.Identity/Program.cs`, and add `"Telemetry": { "OtlpEndpoint": "" }` to `src/PhoneBook.Identity/appsettings.json`. Request bodies (form fields such as `client_secret` and `password`) must never be recorded: do not enable any body enrichment.
- [X] T055 [P] [US3] In `docker-compose.yml`:
  - add the service `aspire-dashboard`:
    - image `mcr.microsoft.com/dotnet/aspire-dashboard:13.5`
    - `profiles: [observability]`
    - ports `18888:18888`
    - environment `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"`, with the comment "DEV-ONLY: no dashboard auth"
  - add `Telemetry__OtlpEndpoint: http://aspire-dashboard:18889` to `identity`, `api` and `api-postgres`. When the profile is off, export fails quietly (FR-017).
  - update the header comment with `docker compose --profile observability up --build`
- [X] T056 [US3] Checkpoint. All US3 tests pass on both providers where relevant, every feature-001 test that checks `traceId` presence still passes, and the build has 0 warnings.

**Checkpoint**: US1, US2 and US3 all work on their own.

---

## Phase 6: User Story 4 - End users sign in to use the phone book (Priority: P3)

**Goal**: OpenIddict authorization code flow with mandatory PKCE:
- a minimal cookie-based login form with antiforgery protection
- seeded DEV-ONLY users (`alice` with read and write, `bob` with read)
- a public client `phonebook-swagger-ui` with implicit consent
- the Swagger UI `authorizationCode` flow

Client credentials stay unchanged (FR-018 to FR-023, research R-05, contract `contracts/identity-signin.md`).

**Independent Test**: Without a browser, run the whole PKCE flow: authorize, login, authorize again, receive the
code, then exchange it for a token. `alice`'s token creates a contact on the API, a wrong `code_verifier` gets
`invalid_grant`, and `bob` gets `403` on POST.

### Tests for User Story 4 ⚠️ (write first; they must fail)

- [X] T057 [P] [US4] Create `tests/PhoneBook.Identity.IntegrationTests/Infrastructure/PkceClient.cs`. It uses `factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true })` and provides:
  - `CreatePkcePair()`: a 43-character random `code_verifier`, and `code_challenge = Base64Url(SHA256(verifier))`
  - `BuildAuthorizeUri(client, redirectUri, scope, challenge, method = "S256", state)`
  - `LoginAsync(returnUrl, user, password)`: GETs the form, extracts the hidden `__RequestVerificationToken` with a regex, and POSTs the form
  - `GetCodeAsync(...)`: follows authorize → login → authorize and returns the `code` from the `Location` query
  - `RedeemAsync(code, verifier, redirectUri)`: posts `grant_type=authorization_code` to `/connect/token`

  In `.../Infrastructure/IdentityFactory.cs`, add the constant `SwaggerRedirectUri = "https://localhost:7001/swagger/oauth2-redirect.html"`. The Development environment loads the seeded users (T068).
- [X] T058 [P] [US4] Create `tests/PhoneBook.Identity.IntegrationTests/AuthorizationCodeFlowTests.cs`, following the contract tables (`contracts/identity-signin.md`):
  - **Discovery**: advertises `authorization_endpoint`, `authorization_code` in `grant_types_supported` and `code` in `response_types_supported`. `code_challenge_methods_supported` is **exactly** `["S256"]` (FR-018).
  - **Anonymous authorize** → `302` to `/account/login?ReturnUrl=…`.
  - **Happy path, alice** (AC1): the token response has `token_type` Bearer and `expires_in` 3600 (FR-026; observed as seconds remaining, so asserted within 3590–3600). The JWT has:
    - `sub` = alice's stable GUID
    - `name`
    - `aud` = `phonebook-api`
    - `scope` = `phonebook.read phonebook.write`
  - **Stable id**: the `sub` is the same across two sign-ins.
  - **Down-scoping** (FR-020): bob requesting both scopes → the token scope is only `phonebook.read`.
  - **Access denied** (FR-020): bob requesting `phonebook.write` only, or a request with no `scope` → `302` to `redirect_uri?error=access_denied&state=…`.
  - **PKCE** (AC2, FR-018): a missing `code_challenge`, or `code_challenge_method=plain` → `302` to `redirect_uri?error=invalid_request`.
    *Implementation note:* OpenIddict validates PKCE before the redirect URI and renders `400 invalid_request` itself (no redirect, no code). The test and contract record the observed behaviour.
  - **Token errors** (FR-019):
    - a wrong `code_verifier` → `400 invalid_grant`
    - a missing `code_verifier` → `400 invalid_grant` (CHK015). If OpenIddict returns a different code, record it and correct the contract row, as in feature 001.
      *Implementation note:* observed `invalid_request` ("The mandatory 'code_verifier' parameter is missing."). The contract was corrected.
    - a second redemption of the same code → `400 invalid_grant`. **Also**, the first access token's store entry (resolved through `IOpenIddictTokenManager` from the JWT's `oi_tkn_id` claim) now has status `revoked`.
  - **Expired code** (FR-026): with `Identity:AuthorizationCodeLifetime=00:00:02` set on the factory, wait 3 s, then redeem → `400 invalid_grant`.
  - **Session expires mid-sign-in** (spec edge case; FR-026; analyze C1): with `Identity:SessionLifetime=00:00:02`, sign in, wait 3 s, then call authorize again with the same cookie → `302` to `/account/login`, and no code is issued.
  - **Return addresses** (FR-024): an unknown `client_id`, or a `redirect_uri` that differs from a registered one even only by a trailing `/` → a `400` from the Identity host, with no `Location` pointing to the requested address.
- [X] T059 [P] [US4] Create `tests/PhoneBook.Identity.IntegrationTests/AccountLoginTests.cs`:
  - `GET /account/login` → `200` HTML with `username`, `password`, a hidden `ReturnUrl` and `__RequestVerificationToken`, plus `Cache-Control: no-store`. The root element has `lang="en"` and `dir="ltr"`, and each input has a matching `<label for=…>` (spec Clarifications).
  - **AC3 / FR-021**:
    - a wrong password for `alice` and an unknown user `nobody` both return `200` with "Invalid username or password." and no authentication cookie
    - the two HTML bodies are identical once the antiforgery token is removed
  - **Timing** (FR-021): the median of 10 attempts for `nobody` is within a factor of 2 of the median of 10 wrong-password attempts for `alice` (research R-08). Tag it `[Trait("Category", "Performance")]`.
  - **Forged submissions** (FR-025): a POST without an antiforgery token → `400`.
  - **Open redirects** (FR-024): `ReturnUrl=https://evil.example/x`, `//evil.example` and `/\evil.example` → after a valid login, a redirect to `/`, never to the external host.
  - **AC6**: `GET /account/register` and `POST /account/register` → `404`. No sign-out endpoint exists either (`/account/logout` → `404`, out of scope).
  - **Cookie** (FR-025, FR-026), with an `https://localhost` client: `HttpOnly`, `SameSite=Lax` and `Secure` are set, and `Expires` is 15 minutes (±1 min) from now.
    *Implementation note:* the cookie is a browser-session cookie with no `Expires`. The 15-minute limit is enforced server-side in the authentication ticket, which the session-expiry test in T058 proves. The test asserts that any `Expires` present is ≤ 16 min.
- [X] T060 [US4] Extend `tests/PhoneBook.Identity.IntegrationTests/TokenRateLimitTests.cs` (T030), using a fresh `RateLimitedIdentityFactory` (limit 3):
  - **Brute-force protection** (constitution v1.0.2, VI; FR-008): 3 × `POST /account/login` with wrong credentials. The 4th → `429`, `Retry-After` ≥ 1, `Content-Type: text/html`, and a body containing the sign-in form and "Too many attempts. Try again in" (FR-009). It is never JSON.
  - **Shared allowance** (FR-008): 2 token requests plus 1 login POST use up the allowance, so the next login POST gets `429`.
  - **No lockout** (FR-008): after the window resets, `alice` signs in successfully.
- [X] T061 [P] [US4] Create `tests/PhoneBook.Identity.IntegrationTests/SeededUsersEnvironmentTests.cs` (FR-027):
  - Use a factory subclass with `builder.UseEnvironment("Production")`, `Identity:Users:0:*` set to `alice` with a plain-text password, and plain-HTTP settings as needed for `/account/login`.
  - Signing in as `alice` fails with the generic message: no users are loaded outside Development.
  - A warning log entry says configured users were ignored, and it does not contain the user name or password (use the Identity project's `LogCapture` from T044).
- [X] T062 [US4] Extend `tests/PhoneBook.Identity.IntegrationTests/EndToEndTokenToApiTests.cs`:
  - **AC1 / AC4 / FR-020**: `alice`'s code-flow token → `POST /api/v1/contacts` gets `201` and `GET ?tag=` gets `200` with the paged body
  - `bob`'s token → POST gets `403` with `errorCode` `Auth.Forbidden`, and GET gets `200`
  - **AC5 / FR-023**: the existing client-credentials tests stay unchanged and green
- [X] T063 [P] [US4] Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/SwaggerDocumentTests.cs` (SQLite collection):
  - `GET /swagger/v1/swagger.json` has an `oauth2` scheme with both `clientCredentials` and `authorizationCode` flows
  - `authorizationUrl` ends with `/connect/authorize` and `tokenUrl` ends with `/connect/token`
  - the ContactPage-shaped response schema is present for `GetContactsByTag`

### Implementation for User Story 4

- [X] T064 [P] [US4] Extend `src/PhoneBook.Identity/IdentitySettings.cs`:
  - `IList<IdentityUserOptions> Users` (new file `src/PhoneBook.Identity/Users/IdentityUserOptions.cs`: `UserName`, `Password`, `DisplayName`, `IList<string> Scopes`)
  - `SwaggerUiClientOptions SwaggerUi` (new file `src/PhoneBook.Identity/Users/SwaggerUiClientOptions.cs`: `ClientId = "phonebook-swagger-ui"`, `DisplayName`, `IList<string> RedirectUris`)
  - `TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5)`, `TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1)` and `TimeSpan SessionLifetime = TimeSpan.FromMinutes(15)` (FR-026). Tests override them, so they are applied through options at runtime (T067).
- [X] T065 [P] [US4] Create `src/PhoneBook.Identity/Users/IdentityUser.cs`, a sealed record in namespace `PhoneBook.Identity.Users` with the fields from data-model §5: `Guid Id`, `string UserName`, `string PasswordHash`, `string DisplayName`, `IReadOnlyList<string> Scopes`. Also create `src/PhoneBook.Identity/Users/StableUserId.cs`, which builds a name-based RFC 4122 v5 GUID (SHA-1) from `UserName.ToUpperInvariant()` using a fixed namespace GUID constant. `Id` becomes the `sub` claim.
- [X] T066 [US4] Create `src/PhoneBook.Identity/Users/InMemoryUserStore.cs`, a singleton:
  - On first use (a lazy, thread-safe build from `IOptions<IdentitySettings>` and `IHostEnvironment`), it hashes every configured password with `PasswordHasher<IdentityUser>.HashPassword` (PBKDF2, HMAC-SHA512, 100,000 iterations; research R-08) and keeps only the hashes. Plain text is never stored.
  - **Outside Development** (FR-027): it loads **no** users. If any are configured, it logs one warning ("Configured users ignored outside Development") with no user names.
  - **`IdentityUser? ValidateCredentials(string userName, string password)`**:
    - user names are compared case-insensitively (`OrdinalIgnoreCase`)
    - for an **unknown** user, still run `VerifyHashedPassword` against a fixed dummy hash, so timing does not reveal whether an account exists (FR-021)
    - it returns `null` on any failure and never throws
  - **`IdentityUser? FindById(Guid id)`**.
  - Register it in `Program.cs`.
- [X] T067 [US4] Update `src/PhoneBook.Identity/Program.cs`:
  - **Cookie authentication** (FR-025, FR-026): `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.LoginPath = "/account/login"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.SlidingExpiration = false; })`. Set `ExpireTimeSpan` from `IdentitySettings.SessionLifetime` through `AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme).Configure<IOptions<IdentitySettings>>(...)`, so tests can shorten it (analyze C1).
  - `AddAntiforgery()`
  - **OpenIddict server**:
    - `SetAuthorizationEndpointUris("connect/authorize")`
    - `AllowAuthorizationCodeFlow()`
    - `RequireProofKeyForCodeExchange()`
    - `.UseAspNetCore().EnableAuthorizationEndpointPassthrough()`
    - keep token and authorization storage enabled (the default), which gives revocation on code replay (FR-019)
  - **S256 only** (FR-018): in the existing `AddOptions<OpenIddictServerOptions>()` configuration, clear `CodeChallengeMethods` and add only `CodeChallengeMethods.Sha256`
  - **Lifetimes** (FR-026): in the same options configuration, set `AuthorizationCodeLifetime` and `AccessTokenLifetime` from `IdentitySettings`
  - **Middleware**: `app.UseAntiforgery()` after `UseAuthorization()`
  - **Endpoints**: map `MapAccountEndpoints()` and `MapAuthorizeEndpoint()`
  - **Unchanged**: client credentials, apart from the explicit 1 h token lifetime, which equals the previous default
- [X] T068 [P] [US4] In `src/PhoneBook.Identity/appsettings.Development.json`:
  - add `Identity:Users`:
    - `alice` / `alice-dev-password` / "Alice (DEV-ONLY)" / both scopes
    - `bob` / `bob-dev-password` / "Bob (DEV-ONLY)" / `phonebook.read`
  - add `Identity:SwaggerUi:RedirectUris`: `https://localhost:7001/swagger/oauth2-redirect.html`, `http://localhost:7001/swagger/oauth2-redirect.html` and `http://localhost:7003/swagger/oauth2-redirect.html`
  - mark every plain-text password DEV-ONLY
  - leave `Users` empty in `src/PhoneBook.Identity/appsettings.json`, so production has no seeded users
- [X] T069 [US4] Create `src/PhoneBook.Identity/Endpoints/LoginPage.cs` and `src/PhoneBook.Identity/Endpoints/AccountEndpoints.cs`:
  - **`LoginPage.Render(HttpContext, string? returnUrl, string? message)`** returns the HTML string for a minimal **English, left-to-right** page (`<html lang="en" dir="ltr">`, spec Clarifications) with a form of `<label>`-ed `username` and `password` fields, a hidden `ReturnUrl` and the antiforgery token from `IAntiforgery.GetAndStoreTokens`. Every dynamic value is `HtmlEncoder`-encoded. It is shared with the rate-limit page (T070).
  - **`MapAccountEndpoints(this IEndpointRouteBuilder)`**:
    - **`GET /account/login?ReturnUrl=`** returns `LoginPage.Render` with `Content-Type: text/html; charset=utf-8` and `Cache-Control: no-store`.
    - **`POST /account/login`**:
      - takes `[FromForm]` fields, with antiforgery validated automatically by minimal APIs (FR-025)
      - calls `InMemoryUserStore.ValidateCredentials`
      - **on success**: `SignInAsync(Cookie, principal with sub and name)`, then `Results.LocalRedirect(safeReturnUrl)`. `safeReturnUrl` is `ReturnUrl` only when it starts with `/` and does not start with `//` or `/\`; otherwise it is `/` (FR-024).
      - **on failure**: re-render with "Invalid username or password." and status `200`
      - `.RequireRateLimiting(TokenRateLimitOptions.TokenPolicy)`
  - Log only the outcome, never the user name or password (FR-016).
  - There is no registration or sign-out endpoint (AC6; sign-out is out of scope).
- [X] T070 [US4] Extend `src/PhoneBook.Identity/RateLimiting/RateLimitingSetup.cs` `OnRejected` (FR-009, research R-08): when the rejected endpoint is `POST /account/login`, write status `429` and `Retry-After`, with `text/html` from `LoginPage.Render(context, returnUrl from the form, "Too many attempts. Try again in {n} seconds.")`. Every other endpoint keeps the OAuth JSON body. Add no try/catch.
- [X] T071 [US4] Create `src/PhoneBook.Identity/Endpoints/AuthorizeEndpoint.cs` with `MapAuthorizeEndpoint()`, mapping `GET` and `POST` `connect/authorize`:
  - Get the request with `context.GetOpenIddictServerRequest()`. OpenIddict has already rejected unknown clients, non-exact `redirect_uri`s and missing or plain PKCE (FR-018, FR-024).
  - Authenticate the cookie scheme. If it fails, or the `sub` user is no longer in `InMemoryUserStore`, return `Results.Challenge(new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + QueryString.Create(Request.HasFormContentType ? Request.Form : Request.Query) }, [Cookie])`.
  - Otherwise build a `ClaimsIdentity` with the OpenIddict authentication type and the claims `sub` (user id) and `name` (display name). Set their destinations to the access token.
  - **Scopes** (FR-020): `granted = request.GetScopes().Intersect(user.Scopes, StringComparer.Ordinal)`. If `granted` is empty, which includes an absent `scope`, return `Results.Forbid(properties with error = access_denied and a description, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme])`. Otherwise call `identity.SetScopes(granted)`.
  - Call `SetResources(settings.Audience)`.
  - Return `Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)`.
  - There is no consent screen, because the client's consent type is Implicit.
- [X] T072 [US4] Extend `src/PhoneBook.Identity/Endpoints/TokenEndpoint.cs` with an `IsAuthorizationCodeGrantType()` branch:
  - authenticate with `OpenIddictServerAspNetCoreDefaults.AuthenticationScheme` to recover the principal stored in the code
  - if its `sub` user no longer exists, return `Forbid` with `invalid_grant`
  - otherwise sign the principal in again
  - leave the client-credentials branch byte-for-byte unchanged (FR-023)
  - OpenIddict itself enforces PKCE verification and single-use codes (FR-019)
- [X] T073 [US4] Extend `src/PhoneBook.Identity/Seeding/IdentitySeeder.cs` to create the public client from `settings.Value.SwaggerUi` idempotently:
  - `ClientType = ClientTypes.Public`, with no secret
  - `ConsentType = ConsentTypes.Implicit`
  - `Permissions`: `Endpoints.Authorization`, `Endpoints.Token`, `GrantTypes.AuthorizationCode`, `ResponseTypes.Code`, `Scope` + `phonebook.read`, and `Scope` + `phonebook.write`
  - `Requirements.Features.ProofKeyForCodeExchange`
  - every configured `RedirectUris` entry
  - skip it when no redirect URIs are configured
- [X] T074 [US4] Update `src/PhoneBook.Api/Infrastructure/Swagger/SwaggerSetup.cs`:
  - add `AuthorizationCode = new OpenApiOAuthFlow { AuthorizationUrl = {PublicAuthority}/connect/authorize, TokenUrl = {PublicAuthority}/connect/token, Scopes = read and write }` next to `ClientCredentials`
  - update the scheme description to mention both flows
  - in the UI options, add `options.OAuthUsePkce()` and change `OAuthClientId` to `phonebook-swagger-ui`, the flow with no secret
  - add a comment saying that client-credentials users now type `phonebook-swagger` and its secret, and record this in the README (T078)
- [X] T075 [US4] Checkpoint. All US4 tests pass, all feature-001 Identity tests stay green (FR-023), and the build has 0 warnings.

**Checkpoint**: All four user stories work on their own.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T076 [P] Add a header comment to `specs/001-phonebook-management/contracts/phonebook-api.openapi.yaml` saying that `GET /api/v1/contacts` is superseded by `specs/002-production-readiness/contracts/phonebook-api-changes.openapi.yaml` (BREAKING, FR-006) and that 429 applies to all business routes (constitution VII: specs are the source of truth).
- [X] T077 [P] Check `specs/002-production-readiness/contracts/identity-signin.md` and `contracts/phonebook-api-changes.openapi.yaml` against the observed behaviour (the error codes from T058, the `Retry-After` format and the flow URLs), and correct any drift.
- [X] T078 Update the documentation:
  - **`README.md`**, a "Feature 002" section:
    - paging, with the **BREAKING** v1 response change, showing the tag-search JSON **before and after** side by side (FR-006)
    - the paging caveat: pages reflect the data when each is requested (E1)
    - rate limits: configuration keys, 429 precedence, the `ForwardedHeaders` note for proxies, and no per-account lockout
    - OpenTelemetry, with the Aspire dashboard 13.5 (`docker compose --profile observability up`), and the error `traceId` format change
    - end-user sign-in (`alice` / `bob`, DEV-ONLY): S256-only PKCE, lifetimes, cookie rules, and users only in Development
  - **`README.md`**, elsewhere:
    - the **deployment requirement** that PostgreSQL uses `LC_COLLATE=C`
    - the Swagger client-id change: `phonebook-swagger-ui` is pre-filled, and client-credentials users type `phonebook-swagger` (I2)
    - the new packages in the technology table
    - "Trade-offs and future work": remove the four delivered items, and add a distributed rate limiter, keyset paging, a production telemetry back end, real user management, sign-out, and `RateLimit-*` headers
    - the updated test counts
  - **`docker-compose.yml`**: update the header comment about Swagger Authorize for the client-id change (I2).
  - **`specs/001-phonebook-management/quickstart.md`**: add a one-line note that feature 002 changed the pre-filled Swagger client id (I2).
- [X] T079 [P] Constitution audit, recording the results in the PR or commit message:
  - `grep -rn "catch" src --include=*.cs` still shows only the four allowed sites (Principle II)
  - every `429` and `400` from the API is ProblemDetails with `errorCode` (Principle III)
  - no provider-specific SQL or `COLLATE` in `src/PhoneBook.Application` (Principle IV)
  - every plain-text credential is marked DEV-ONLY (Principle VI)
- [ ] T080 Run [quickstart.md](./quickstart.md) §1 (automated) and walk through the manual scenarios 1–20 in §2, including a compose run with `--profile observability`:
  - *Status (2026-09-25):* §1 automated is done (219/219, Release, 0 warnings). The manual scenarios 1–20 are **pending**: they need the compose stack and a person signing in through Swagger UI in a browser.
  - **#11**: the API's discovery and JWKS calls to Identity appear in the same trace as the Identity server spans (FR-012).
  - **#13**: the median latency, with and without the dashboard container, is within 5% (SC-005).

  Record the results in the quickstart, as was done for feature 001.
- [X] T081 Final checkpoint. `dotnet build PhoneBook.slnx -c Release` gives 0 warnings, and `dotnet test --solution PhoneBook.slnx -c Release` is all green on both providers. Confirm that CI (`.github/workflows/ci.yml`) needs no change, beyond checking that the Docker-based PostgreSQL tests still run with `--locale=C`. Push only when the user asks.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** has no dependencies.
- **Foundational (Phase 2)** depends on Setup and blocks every story:
  - T007 and T009 (collation) are needed for US1 on PostgreSQL
  - T010 (high test limits and the unsealed `IdentityFactory`) is needed before US2 lands, or the feature-001 load tests fail
  - T011 and T012 (telemetry names and per-host metrics) are used by US2's rejection counter and by US3
- **US1 (Phase 3), US2 (Phase 4), US3 (Phase 5) and US4 (Phase 6)** each depend only on Foundational.
- **Polish (Phase 7)** depends on every story it documents.

### User Story Dependencies

- **US1 (P1)**: independent. Its stub task (T016) comes first within the story.
- **US2 (P1)**: independent. T028 implements `Capture` from T038. If US2 is built before US3, add the `Capture` property to `IPhoneBookApiFactory` as part of T028. The login rate-limit test (T060) is in US4, because the login endpoint arrives there.
- **US3 (P2)**: independent. The rate-limit assertions in T040 and T044 need US2. If US3 is built before US2, skip those asserts until US2 lands.
- **US4 (P3)**: independent of US1 to US3. T061 uses the Identity test project's `LogCapture` from T044 (US3); if US4 is built before US3, create that file as part of T061. T062's assertion on the paged body assumes US1. Before US1, assert only on `200`.

### Shared-file hotspots (serialise edits)

- `src/PhoneBook.Api/Program.cs`: T033 (US2), T051 and T053 (US3)
- `src/PhoneBook.Identity/Program.cs`: T012, T035 (US2), T054 (US3), T067 (US4)
- `src/PhoneBook.Identity/RateLimiting/RateLimitingSetup.cs`: T035 (US2), T070 (US4)
- `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQueryHandler.cs`: T016 and T023 (US1), T048 (US3)
- `src/PhoneBook.Api/Endpoints/Contacts/GetContactsByTag.cs`: T016 and T025 (US1)
- `tests/.../IPhoneBookApiFactory.cs` and the API factories: T010, T028, T038
- `tests/.../IdentityFactory.cs`: T010, T057
- `tests/.../TokenRateLimitTests.cs`: T030 (US2), T060 (US4)
- `docker-compose.yml`: T009, T055, T078

### Within Each User Story

1. Add stubs for any new signature, so the solution compiles.
2. Write the tests and see them fail on assertions.
3. Add options and DTOs, marked [P].
4. Wire up services and handlers.
5. Update the endpoints and the host pipeline.
6. Reach the checkpoint: build with 0 warnings and run the full suite.

### Parallel Opportunities

- **Setup**: T002, T003, T004 and T005.
- **Foundational**: T008, T009, T010, T011 and T012, after T007.
- **US1**: T014 and T015, then T016. After that, the tests T017, T018 and T021.
- **US2**: T028, T029 and T030 (after T027); T031, T034 and T036.
- **US3**: after T038, the tests T039, T040, T041, T042, T043, T044 and T045, which are all in different files; then T050, T051, T054 and T055.
- **US4**: tests T057, T058, T059, T061 and T063; T064, T065 and T068.
- **Across stories**, once Phase 2 is done: US1 and US4 touch no common files, so two developers or agents can take them at the same time. US2 and US3 share the `Program.cs` hotspots and the test factories.

---

## Parallel Example: User Story 1

```text
# Stubs first (US1):
T014 PagingDefaults.cs   T015 PagedResponse.cs   → T016 query signature + handler/endpoint stub

# Then the tests together:
T017 GetContactsByTagPagingTests.cs (both providers)
T018 GetContactsByTagEndpointTests.cs (validation)
T021 SchemaIndexTests.cs
```

## Parallel Example: User Story 3

```text
# After T038 (TelemetryCapture + factory wiring), all test files at once:
T039 TracingTests.cs   T040 MetricsTests.cs   T041 PersonalDataTelemetryTests.cs
T042 TelemetryResilienceTests.cs   T043 OutgoingCallTracingTests.cs   T044 Identity TelemetryTests.cs
T045 LayerDependencyTests.cs
```

## Parallel Example: User Story 4

```text
T058 AuthorizationCodeFlowTests.cs   T059 AccountLoginTests.cs   T063 SwaggerDocumentTests.cs
T064 IdentitySettings + options   T065 IdentityUser + StableUserId   T068 appsettings.Development.json
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup, then Phase 2 Foundational (the collation, the test limits, and the telemetry names and metrics).
2. Phase 3 (US1 paging), starting with the stubs.
3. **Stop and validate**: run quickstart scenarios 1–5 on both providers. The unbounded-result risk is gone, and
   this can ship.

### Incremental Delivery

1. **US1** (paging) removes the biggest production risk.
2. **US2** (rate limiting) protects the API and the token endpoint.
3. **US3** (observability) shows operators what the service is doing.
4. **US4** (end-user sign-in) prepares for real users.

Each step ends with a green checkpoint and can be committed on its own.

### Parallel Team / Multi-Agent Strategy

After Phase 2, run US1 and US4 in parallel, since they share no files. Then do US2 followed by US3, because
they share the `Program.cs` hotspots.

---

## Notes

- **[P]** means different files and no dependency on an unfinished task.
- **The breaking change is intentional** (FR-006). Only the tag-search tests change shape (T019), and their
  expected contacts and order stay the same (SC-008).
- **Performance tests** (T020, T042) carry `[Trait("Category", "Performance")]`, so CI can filter them if the
  runners are slow.
- **Commits**: commit after each checkpoint, with no Co-Authored-By trailer (constitution v1.0.2, workflow gate 6). Push only on request.

---

## Phase 8: Convergence

- [X] T082 After the user approves pushing the feature-002 commits, confirm the GitHub Actions run of `.github/workflows/ci.yml` is green with all 220 tests, including the 100,000-contact SC-001 performance tests on both providers. If the 500 ms bound is unstable on the hosted runner, handle it explicitly (for example, run `[Trait("Category", "Performance")]` tests in a separate CI job) and document it in the README, instead of weakening SC-001. Per T081 / SC-001 / constitution gate 5 (partial)
  - *Done 2026-09-25:* GitHub Actions run 36166483204 is green, 220/220, including the SC-001 performance tests on both providers. No CI changes were needed.
- [X] T083 Build `InMemoryUserStore` when the Identity host starts, not on the first sign-in request: hash the configured passwords at start-up, and outside Development log the "Configured users ignored outside Development" warning at start-up. For example, resolve the store and force its user map in `src/PhoneBook.Identity/Seeding/IdentitySeeder.cs` `StartAsync`. Add a test in `tests/PhoneBook.Identity.IntegrationTests/SeededUsersEnvironmentTests.cs` that the Production warning appears after start-up with no sign-in request. Per data-model §5 ("PasswordHash … at start-up") / research R-08 (partial)
