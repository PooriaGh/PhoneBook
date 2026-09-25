# Research: Phone Book Management

**Feature**: `001-phonebook-management` | **Date**: 2026-09-25 | **Plan**: [plan.md](./plan.md)

Each section records a decision, why it was made, and which alternatives were rejected. The goal of this project (from the user input to `/speckit-plan`) is to **demonstrate professional backend skills** while staying within the scope of the spec. So the bar is a pattern that is justified and used correctly, not just a pattern that is present.

---

## R-01 Runtime and language

- **Decision**: Use .NET 10 (LTS) and C# 14, with `Nullable` enabled and `TreatWarningsAsErrors` turned on. Use Central Package Management (`Directory.Packages.props`), shared `Directory.Build.props` settings and the `.slnx` solution format that is already in the repository.
- **Rationale**: The brief requires ".NET Core". .NET 10 is the current LTS, and SDK 10.0.400 is installed on the development machine. Central Package Management keeps package versions identical across the 10 projects.
- **Alternatives**: .NET 8 LTS is older and still supported, but has no advantage here. .NET 9 is STS and out of support in 2026.

## R-02 "In-memory database", Dapper, Testcontainers and Respawn in one design ⚠️ key decision

These four requirements conflict:

| Requirement | Works with EF Core `InMemory` provider? |
|---|---|
| "use in memory database" (brief + user) | ✅ |
| Dapper queries (raw SQL over `DbConnection`) | ❌ The InMemory provider has no SQL engine and no `DbConnection` |
| Respawn (resets the database between tests) | ❌ Needs a real relational database: SQL Server, PostgreSQL, MySQL, Oracle and a few others |
| Testcontainers (a real database in Docker for tests) | ❌ Pointless without a real database |

- **Decision**: Put the database behind a **provider switch** (`Database:Provider`):
  1. **`Sqlite` (default)**: an **in-memory SQLite** database (`Data Source=phonebook;Mode=Memory;Cache=Shared`). A singleton "keep-alive" connection holds it open for the lifetime of the process. This satisfies "In Memory" from the brief: nothing reaches disk, data is lost on restart, and nothing needs to be installed. It is still a real SQL engine, so **Dapper works**.
  2. **`Postgres`**: PostgreSQL. It is used by **integration tests through Testcontainers and Respawn** and by the optional `docker compose` profile.
  - The schema uses **snake_case** names (via `EFCore.NamingConventions`) and simple ANSI SQL, so the **same Dapper SQL runs unchanged on SQLite and PostgreSQL**.
  - The schema is created with `EnsureCreated()` at startup. Migrations are unnecessary for a database that disappears on every restart, and EF migrations are provider-specific.
  - **Write gate (revision 2, finding P2)**: For `Provider=Sqlite` only, a singleton `SqliteWriteGate` (`SemaphoreSlim(1,1)`) is held around `base.SaveChangesAsync` in `WriteDbContext`. Concurrent writers on a shared-cache in-memory database can otherwise fail with `SQLITE_LOCKED`, which would surface as a 500 and break FR-017 and SC-005. Reads are not gated. PostgreSQL uses MVCC and needs no gate.
- **Rationale**: This is the only design that meets every requirement at once. It also shows the Dependency Inversion Principle clearly: Application and Domain have no idea which provider is used.
- **Alternatives rejected**:
  - *EF Core InMemory only*: Dapper, Respawn and Testcontainers would be impossible.
  - *PostgreSQL only*: breaks "in-memory, no persistence needed" and makes Docker mandatory just to run the app.
  - *Hand-written `ConcurrentDictionary` repositories*: no Unit of Work, no ReadDbContext, no Dapper.
  - *SQLite for tests too*: Respawn does not support SQLite, and Testcontainers would add nothing.

## R-03 Architecture style

- **Decision**: Clean Architecture with DDD tactical patterns:
  `SharedKernel ← Domain ← Application ← Infrastructure ← Api` (the arrows point toward what each layer depends on). A separate host, `Identity`, acts as the authorization server.
- **Rationale**: This is the layering reviewers expect when a brief asks for DDD. The dependency rule is enforced automatically by architecture tests (R-15).
- **Alternatives**: Vertical-slice architecture works well for CRUD, but it hides the aggregate, repository and Unit of Work story the user wants to showcase.

## R-04 Aggregate design

- **Decision**: **`Contact` is the aggregate root**. The phone book is *not* an aggregate root. Value objects are `ContactId` (a strongly-typed `Guid` v7), `PersonName` (first and last name), `PhoneNumber` and `Tag`.
- **Rationale**: An aggregate is a consistency boundary. No invariant spans more than one contact except the duplicate rule (R-06), and a domain service handles that. A single `PhoneBook` aggregate holding every contact would make every write load the whole collection, and concurrent edits to different contacts would conflict.
- **Alternatives**: A `PhoneBook` aggregate with child `Contact` entities was rejected: it is a large-aggregate anti-pattern and scales poorly.

## R-05 Result pattern (no exceptions for control flow)

- **Decision**: Write a small custom `Result` / `Result<T>` and `Error(Code, Description, ErrorType)` in `SharedKernel`. `ErrorType` has the values `Validation`, `NotFound`, `Conflict`, `PreconditionFailed` and `Unexpected`. Domain factories and methods return `Result`, never `throw`. The API maps `ErrorType` to RFC 9457 ProblemDetails.
- **Rationale**: Writing it by hand (about 80 lines) shows understanding instead of a library dependency. It also allows `static abstract` factory members, which the generic pipeline behaviours need to build failure results without reflection (R-08).
- **Alternatives**: `ErrorOr`, `FluentResults` and `Ardalis.Result` are all fine libraries, but they hide the mechanics the user wants to demonstrate.

## R-06 Domain service: duplicate-contact rule

- **Decision**: Add the domain service **`ContactDuplicateChecker`**, with the rule *"the same phone number cannot be registered twice under the same tag"*. It runs on create and on edit (excluding the contact being edited). It depends on a domain-owned port, `IContactUniquenessReader`.
- **Rationale**: The user asked for domain services "if needed". This is the one rule in the phone book that one aggregate cannot check on its own, which is the textbook reason for a domain service. It stays consistent with the spec: the same number *may* appear under different tags. The spec has been updated to match.
- **Alternatives**: A unique index alone would turn a business rule into a database exception, which also contradicts "do not throw". The index is still added as a safety net.

## R-07 CQRS: write side vs read side

- **Decision**:
  - **Write side**: `WriteDbContext` (EF Core, change tracking) is used only through `IContactRepository` and `IUnitOfWork`, and works only with the `Contact` aggregate.
  - **Read side**: `ReadDbContext` (EF Core, `QueryTrackingBehavior.NoTracking`) maps flat **read models** (`ContactReadModel`) onto the same table. It is exposed through `IReadDbContext`.
  - **Dapper** runs through `ISqlConnectionFactory` and serves the tag search (`GetContactsByTagQuery`).
  - `GetContactByIdQuery` uses `ReadDbContext`/LINQ, and `GetContactsByTagQuery` uses Dapper, so **both read technologies appear and are both tested**.
  - **Ordering (revision 2, finding P1)**: The Dapper SQL has **no `ORDER BY`**. SQLite compares text as raw bytes by default, while PostgreSQL uses the database locale, so Persian names would sort differently on the two providers. The handler sorts in memory with `StringComparer.Ordinal` (last name, then first name), which is deterministic on both. The spec (FR-008) states "character-code order".
- **Rationale**: This shows the usual reason for CQRS in .NET: a rich model for writes and lean, fast projections for reads. The two read paths let reviewers compare EF no-tracking queries with Dapper.
- **Alternatives**: A separate read store kept up to date by domain-event projections is real "eventual consistency CQRS". It was rejected as unnecessary complexity for one in-memory table, and is noted as a future extension in the README.

## R-08 Mediator pipeline and error handling

- **Decision**: Use **MediatR 12.5.0**, the last Apache-2.0 release. Later versions need a commercial license key, which would make the project log license warnings. Pipeline behaviours run in this order, from outermost to innermost:
  1. `UnhandledExceptionBehavior`: catches *any* exception, logs it with the request name, and returns `Result.Failure(Error.Unexpected)`. This is the **second safety net**.
  2. `LoggingBehavior`: structured start and end log entries with the elapsed time, logged as a warning when a request is slow.
  3. `ValidationBehavior`: runs FluentValidation validators and **returns** a validation failure. It never throws.
  4. `UnitOfWorkBehavior` (commands only, marker `ICommand`/`ICommand<T>`): calls `IUnitOfWork.SaveChangesAsync` only when the handler result is a success.
  - `TResponse` is constrained to `IResultBase`, which has a `static abstract` failure factory. This avoids reflection.
  - **Global exception handling**: an ASP.NET Core `IExceptionHandler` (`GlobalExceptionHandler`) turns anything that escapes (model binding, middleware, auth) into a `500` ProblemDetails with a `traceId`, and never leaks stack traces outside Development. This is the **first safety net**.
- **Rationale**: This is exactly what the user asked for: "handle any unhandled exception in mediatR pipeline and global exception middleware". Having both layers means no exception reaches the client as an unformatted error.
- **Alternatives**: `martinothamar/Mediator` (source-generated and faster) was rejected because the user asked for MediatR by name. MediatR 13+ has licensing concerns.

## R-09 Unit of Work and domain events

- **Decision**: `WriteDbContext` implements `IUnitOfWork`. `SaveChangesAsync` does four things in order:
  1. Collects domain events from tracked aggregates.
  2. Clears them from the aggregates.
  3. Saves the changes.
  4. **Publishes** the events in-process through `IPublisher`, *after* a successful commit.
  - Domain events are `ContactCreatedDomainEvent`, `ContactUpdatedDomainEvent`, `ContactTagChangedDomainEvent` and `ContactDeletedDomainEvent`. Handlers log an audit trail.
  - The domain layer knows nothing about MediatR. Application wraps each `IDomainEvent` in `DomainEventNotification<T>`, so the Domain has no dependency on MediatR.
- **Rationale**: Publishing after commit avoids announcing changes that were never saved. Keeping the Domain free of MediatR keeps it pure.
- **Alternatives**: A transactional outbox is the production-grade answer and is documented as future work. Dispatching before commit risks side effects for failed transactions.

## R-10 Optimistic concurrency and HTTP ETags

- **Decision**: `Contact` has an integer `Version`, configured as an EF concurrency token and incremented by the aggregate on each change. This works on both providers, unlike SQL Server's `rowversion`.
  - `GET` returns `ETag: "<version>"`.
  - `PUT` and `DELETE` accept an optional `If-Match` header, and answer `412 Precondition Failed` when it does not match.
  - A lost race produces `DbUpdateConcurrencyException`. The repository/UoW catches it and turns it into `Error.Conflict`, which becomes `409`.
- **Rationale**: This covers spec FR-017 and SC-005 (consistency under concurrent requests), and HTTP-correct concurrency control is a strong REST signal.

## R-11 API style

- **Decision**: ASP.NET Core **Minimal APIs**. Each endpoint is a class that implements `IEndpoint` and is found by assembly scanning. They are grouped under `/api/v{version}/contacts` with **Asp.Versioning.Http**.
  - Errors use RFC 9457 **ProblemDetails**, and validation errors use `ValidationProblemDetails`.
  - **Swashbuckle** provides the Swagger UI, including the OAuth2 client-credentials flow, so the reviewer can get a token from inside Swagger.
  - Every operation has typed `Produces<T>`/`ProducesProblem` metadata so the OpenAPI document is accurate.
- **Rationale**: Minimal APIs are the modern default. The brief explicitly says "Swagger", and Swashbuckle's UI supports OAuth flows directly.
- **Alternatives**: Controllers would also be fine. Using `Microsoft.AspNetCore.OpenApi` with Scalar would drop the "Swagger" wording the brief uses.

## R-12 Identity: a separate OpenIddict authorization server

- **Decision**: A separate host, `PhoneBook.Identity`, runs **OpenIddict 7.x** (server with the ASP.NET Core and EF Core stores).
  - **Flow**: OAuth 2.0 **client credentials**. The API is a machine-to-machine resource, and there are no end users in the spec.
  - **Scopes**: `phonebook.read` and `phonebook.write`, with audience/resource `phonebook-api`.
  - **Seeded clients**: `phonebook-swagger` (both scopes, for Swagger UI) and `phonebook-readonly` (read scope only, used to demonstrate `403`).
  - **Store**: the OpenIddict EF Core store on the same kind of in-memory SQLite, with its own database name.
  - **Keys**: ephemeral signing and encryption keys, with access-token encryption **disabled** so the API can validate JWTs from the discovery document. Production would use X.509 certificates, and the README says so.
  - **API side**: `OpenIddict.Validation.AspNetCore` plus `System.Net.Http` integration, which reads the issuer's discovery document and JWKS. Authorization policies are `Contacts.Read` (GET) and `Contacts.Write` (POST, PUT, DELETE).
- **Rationale**: This is the separate identity app the user asked for. Client credentials is the correct grant for service-to-service calls. Two scopes make authorization testable (`401` vs `403`).
- **Swagger in the browser (revision 2, findings U1 and U5)**:
  - **CORS**: The Identity host enables a CORS policy for the origins in `Identity:AllowedCorsOrigins`, limited to `POST` and `OPTIONS` on `/connect/token`. Without it, the Swagger UI's token request from the API origin is blocked by the browser.
  - **Fixed issuer**: The issuer is set with `SetIssuer(Identity:Issuer)`, so the `iss` claim does not depend on the request host.
  - **Two URLs on the API side**: `Auth:Authority` is the issuer and the discovery URL, reachable server-to-server (for example `http://identity:8080/` in compose). `Auth:PublicAuthority` is the browser-reachable URL, used only for Swagger's `tokenUrl`.
- **Alternatives**: Authorization code with PKCE and a login UI would need user accounts, which is out of scope. Duende IdentityServer needs a commercial license. Keycloak is not a .NET library and not what the user asked for.

## R-13 Validation

- **Decision**: Validation happens in two layers.
  1. **FluentValidation** request validators in Application reject malformed input *shape*: null, blank or too-long values. They run in `ValidationBehavior`.
  2. **Domain value objects** (`PhoneNumber.Create`, `Tag.Create`, `PersonName.Create`) enforce the business invariants and return `Result`.
  - Domain rules are the source of truth and are unit-tested. The validators give fast, per-field error messages at the edge.
- **Phone rule** (spec FR-004): an optional leading `+`, then digits with optional space or `-` separators, and 4–15 digits once separators are removed. Persian digits (U+06F0–06F9) and Arabic-Indic digits (U+0660–0669) are mapped to ASCII first. The normalized value is stored.
- **Tag rule** (spec FR-008): trimmed, 1–50 characters. `NormalizedValue = Trim().ToUpperInvariant()` is stored in the `normalized_tag` column, which has an index. This gives case-insensitive equality that is portable across SQLite and PostgreSQL without collations.

## R-14 Testing strategy (as directed by the user)

| Project | Kind | Scope | Tools |
|---|---|---|---|
| `PhoneBook.Domain.UnitTests` | Unit | **Only domain business rules**: value objects, aggregate behaviour and events, domain service | xUnit v3, Shouldly, NSubstitute (for the domain-service port) |
| `PhoneBook.Api.IntegrationTests` | Integration | Commands and queries (through `ISender`), HTTP endpoints, auth policies, ETags, concurrency, error mapping | xUnit v3, `WebApplicationFactory<Program>`, **Testcontainers.PostgreSql**, **Respawn**, Shouldly, Bogus |
| `PhoneBook.Identity.IntegrationTests` | Integration | Token endpoint (valid, invalid client, unknown scope), discovery document, **end-to-end token → API call** | xUnit v3, `WebApplicationFactory`, Shouldly |
| `PhoneBook.ArchitectureTests` | Architecture | Layer dependency rules and naming conventions | NetArchTest.Rules |

- One PostgreSQL container is started per test run through an xUnit **assembly fixture**. Respawn resets the tables before each test, which keeps tests fast (one container) and isolated (reset per test).
- Authentication in API tests: a `TestAuthHandler` replaces OpenIddict validation. The scopes a test gets are controlled per test through a header, so `401` and `403` paths are covered. The real OpenIddict round trip is covered by the end-to-end test in the Identity project. That test points the API's OpenIddict validation `HttpClient` at the Identity `TestServer` handler.
- **Assertions**: Shouldly, because FluentAssertions 8+ is commercially licensed.
- **Alternatives**: SQL Server Testcontainers works too, but PostgreSQL starts faster and its image is smaller.

## R-15 Cross-cutting and "developer experience" tooling

| Concern | Choice |
|---|---|
| Logging | Serilog (console, structured, request logging, `TraceId` enrichment) |
| Health | `/health/live` and `/health/ready` (checks the database) for both hosts |
| Config | Options pattern with `ValidateOnStart` for `DatabaseOptions` and `AuthOptions` |
| Containers | A `Dockerfile` per host plus `docker-compose.yml` (api, identity, and an optional postgres profile) |
| Code style | `.editorconfig` and analyzers (`AnalysisLevel=latest-recommended`) |
| CI (optional) | GitHub Actions workflow: restore, build and test. Testcontainers works on `ubuntu-latest` |

## R-17 Scope of the error contract (constitution Principle III, finding A2)

- **Decision**: "Every error response MUST be ProblemDetails with `errorCode`" applies to **`PhoneBook.Api` business routes and framework-generated statuses on the API host**: 400, 401, 403, 404, 405, 409, 412 and 500.
  - Body-less statuses from authentication and routing are turned into ProblemDetails by `AddProblemDetails(CustomizeProblemDetails)` and `UseStatusCodePages()`, with `errorCode` defaulting to `Auth.Unauthorized`, `Auth.Forbidden`, `General.NotFound`, `General.MethodNotAllowed` or `General.Error`.
  - **Out of scope, because they follow their own standards**: OAuth 2.0 errors on the Identity host (RFC 6749 §5.2 JSON, such as `invalid_client`) and health-check responses (plain `Healthy`/`Unhealthy`).
- **Rationale**: Clients of the business API get one error format, and interoperability with OAuth clients and orchestrators is kept.
- **Follow-up**: ✅ Fulfilled by constitution **v1.0.1**, whose Principle III now states this scope explicitly (finding V2).
- **Real token challenges (finding U6)**: OpenIddict validation handles the 401 challenge itself and may write its own body, in which case `UseStatusCodePages` would not run. A validation event handler for `ProcessChallengeContext` writes the ProblemDetails (with `errorCode = Auth.Unauthorized`), keeps the `WWW-Authenticate` header, and marks the request handled. A `403` from the authorization middleware has an empty body, so status-code pages handle it. The end-to-end test with a real token checks both. The exact OpenIddict 7.x handler API must be confirmed against the installed package version before implementing.

## R-18 Decisions made during implementation (reconciled after `/speckit-converge`)

- **Validators call the domain factories** (tasks T072, T090). The task text asked for FluentValidation `NotEmpty`/`MaximumLength` rules that use the domain's length constants.
  - **Decision**: `ContactFieldRules` (`src/PhoneBook.Application/Contacts/ContactFieldRules.cs`) calls `PersonName.Create`, `PhoneNumber.Create` and `Tag.Create`, and turns their `FieldError`s into validation failures. The only extra rule is a 64-character limit on the raw phone input (`PhoneNumber.TooLong`).
  - **Rationale**: This goes further than finding D1 asked. No rule or error code is written twice, so the validator and the domain cannot disagree. It also reports *every* invalid field at once, as FR-012 requires. Shape-only rules would have stopped at the first empty field and never reported, for example, `PhoneNumber.InvalidCharacters` alongside a blank last name.
  - **Alternatives**: separate FluentValidation rules that repeat the domain rules. Rejected because the rules would drift apart over time.
- **The end-to-end token test supplies the issuer configuration directly** (task T107). The task text suggested pointing the API's `HttpClient` at the Identity test server's handler.
  - **Decision**: the test reads the Identity host's discovery and JWKS documents, then sets `OpenIddictValidationOptions.Configuration` (issuer + signing keys) on the API under test.
  - **Rationale**: validation stays real (real tokens, real keys, real OpenIddict validation) but needs no network and does not depend on how OpenIddict wires its internal HTTP client.
  - **Coverage of the HTTP discovery path**: the quickstart run against both live HTTPS hosts (17/17) and the Docker Compose smoke test (plain HTTP, issuer `http://identity:8080/`).
- **`Auth:RequireHttpsMetadata` removed** (tasks T055, T119, T127). OpenIddict 7.7.1 validation has no switch for requiring HTTPS metadata; its HTTP handlers accept both `http` and `https` URLs (`RequireHttpUri` filter). The option was bound from configuration but never read, so it was removed instead of being kept as configuration with no effect. Plain-HTTP operation in compose needs only `DisableTransportSecurityRequirement()` on the Identity host in Development.
- **Test runner** (research R-14): the .NET 10 SDK requires Microsoft.Testing.Platform for `dotnet test` (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`). Filters use `--filter-class`/`--filter-method`/`--filter-query` instead of VSTest `--filter`. CI's `--report-trx --coverage` needs the `Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage` packages (task T125).

## R-16 Environment findings

- SDKs 10.0.300 and 10.0.400 are installed. ✅
- Docker CLI is installed, but **the daemon was not running** when this plan was written. Integration tests need Docker Desktop to be running. `quickstart.md` says so.
