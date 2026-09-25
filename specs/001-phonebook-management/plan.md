# Implementation Plan: Phone Book Management

**Branch**: `001-phonebook-management` | **Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-phonebook-management/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

This is a phone book REST API: add, edit, find-by-tag and delete contacts. It is built as a **skills-showcase backend** for a job application, so the plan uses as many professional .NET techniques as the scope honestly justifies:

- **DDD**: a `Contact` aggregate root, value objects, domain events and a domain service for the duplicate rule
- **Clean Architecture** layering
- **CQRS with MediatR**:
  - commands go through a repository and Unit of Work over `WriteDbContext`
  - queries use a read-only `ReadDbContext` with read models, **and Dapper** for tag search
- the **Result pattern** everywhere, with no exceptions for control flow
- **two layers of unhandled-exception protection**: a MediatR pipeline behaviour and a global `IExceptionHandler`
- a **separate OpenIddict authorization server** that issues client-credentials tokens with scopes

Data is kept **in memory** (in-memory SQLite, as the brief requires). The same schema also runs on **PostgreSQL**, which **Testcontainers and Respawn** use for integration tests. The work finishes with a **README that doubles as the cover letter**. It describes the architecture and the AI-assisted, **Spec-Driven Development** process used to build the project.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS). SDK 10.0.400 is installed.

**Primary Dependencies**:
- ASP.NET Core Minimal APIs
- MediatR 12.5.0 (the last Apache-2.0 release)
- FluentValidation
- EF Core 10 (Sqlite and Npgsql providers, EFCore.NamingConventions)
- Dapper
- OpenIddict 7.x (server with EF Core stores in Identity; validation in the API)
- Asp.Versioning.Http
- Swashbuckle.AspNetCore (Swagger UI with OAuth2)
- Serilog.AspNetCore
- AspNetCore.HealthChecks

**Storage**:
- **Default**: in-memory SQLite (a shared-cache named in-memory database, kept alive by a singleton connection), so nothing is persisted (FR-016).
- **Also supported**: PostgreSQL, selected with `Database:Provider`, used by tests and compose.
- The Identity host uses its own in-memory SQLite database for the OpenIddict stores.

**Testing**:
- xUnit v3, Shouldly, NSubstitute and Bogus
- `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- Testcontainers.PostgreSql and Respawn
- NetArchTest.Rules

**Target Platform**: A cross-platform server (Windows dev machine, Linux containers)

**Project Type**: A web service: a REST API plus a separate identity (OAuth2) server

**Performance Goals**:
- A tag search over 10k contacts returns in under 1 s (SC-004), which the indexed `normalized_tag` column delivers.
- Each request takes well under 100 ms locally.

**Constraints**:
- No exceptions for expected failures (Result pattern)
- Stay consistent under concurrent requests: optimistic concurrency with `version`, `409`/`412` (FR-017, SC-005)
- Full Unicode and Persian support, including normalization of Persian digits
- The SQL must be portable between providers, with no reliance on the database for text ordering. Tag-search results are sorted in code with ordinal comparison (R-07).
- The SQLite provider serializes `SaveChanges` through one process-wide write gate, to avoid shared-cache lock failures (R-02).
- No stack traces in responses outside Development

**Scale/Scope**:
- One bounded context (Contacts) and one aggregate
- 5 endpoints plus health checks, and 1 identity host
- About 10 projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates come from **`.specify/memory/constitution.md` v1.0.1** (ratified 2026-09-25; amended the same day to clarify the scope of Principle III). This check was re-run on 2026-09-25 against v1.0.1. It replaces the interim gates G1–G10 (analysis finding G1), and the earlier run against v1.0.0. A "Pre" mark means the principle was satisfied before Phase 0; a "Post" mark means it was satisfied after the Phase 1 re-check.

| Principle | Pre | Post | How the design complies (evidence) |
|---|---|---|---|
| **I. DDD with a rich domain model** | ✅ | ✅ | **Aggregate:** `Contact` is the only aggregate root, and each command changes one instance. **Value objects:** `ContactId`, `PersonName`, `PhoneNumber` and `Tag`, each created through a validating factory. **Domain events:** published after commit (R-09). **Domain service:** `ContactDuplicateChecker` for FR-018, with the port `IContactUniquenessReader` owned by the Domain (R-06). **Purity:** Domain and SharedKernel have no MediatR, EF or ASP.NET references, which the architecture tests enforce. Evidence: data-model §1, R-04. |
| **II. Result pattern (non-negotiable)** | ✅ | ✅ | **Results:** every factory, method, validator, behaviour and handler returns `Result`/`Result<T>` (R-05). **Field errors:** validation errors are `ValidationError` with a list of `FieldError`s. **Translation:** a concurrency conflict or a unique-key violation is turned into a `Result` inside `WriteDbContext`. **Catch sites:** try/catch appears only in the four places the constitution allows (R-08, R-09). One documented exception: `OperationCanceledException` is re-thrown when the client cancels, because cancellation is not a business failure. |
| **III. Two safety nets** | ✅ | ✅ | **The two nets:** `UnhandledExceptionBehavior` and `GlobalExceptionHandler` (R-08). **ProblemDetails:** every API error, including 401, 403, 404 and 405 with no body, becomes ProblemDetails with `errorCode` and `traceId`, through `CustomizeProblemDetails` and `UseStatusCodePages` (R-17). **Scope:** this matches Principle III as amended in v1.0.1: business routes and framework statuses on the API host are covered, while `/health/*` and the OAuth endpoints are exempt (R-17). **Real challenges:** OpenIddict's own 401 challenge is also turned into ProblemDetails, and the end-to-end test checks it. |
| **IV. CQRS** | ✅ | ✅ | **Write side:** repository plus a Unit of Work (`UnitOfWorkBehavior`). **Read side:** `ReadDbContext` (no tracking, read models) and Dapper (R-07). **Portable SQL:** raw SQL is parameterised and runs unchanged on SQLite and PostgreSQL. It has no `ORDER BY`, because the two providers collate text differently; ordering is done in code with ordinal comparison (R-07, finding P1). |
| **V. Test discipline (non-negotiable)** | ✅ | ✅ | **Unit tests:** domain only. **Integration tests:** WebApplicationFactory, Testcontainers PostgreSQL and Respawn. **SQLite:** a smoke suite covers the default in-memory provider (R-14). **Test-first:** domain unit tests are written *before* the foundational domain code (reflected in tasks rev. 3+: Phase 2 T017–T022 and T032; finding K1). **Full suite:** each checkpoint runs the whole solution's tests (finding K2). **Architecture tests:** they enforce the layering. |
| **VI. Secure by default** | ✅ | ✅ | **Authorization:** every business route requires a policy (`Contacts.Read` or `Contacts.Write`). **Malformed ids:** the fallback route requires an authenticated client, so anonymous callers get 401 (finding K3). **Anonymous routes:** only `/health/*` and `/swagger`. **Tokens:** issued by the separate OpenIddict host, with read and write scopes (R-12). **Secrets:** only development secrets are committed, and each is marked `DEV-ONLY`. **Concurrency:** `version` token, `If-Match` → 412, lost race → 409 (R-10). |
| **VII. Spec-driven, AI-assisted delivery** | ✅ | ✅ | **Spec Kit flow:** followed in full, including analyze and this re-plan. **Spec sync:** during this planning run the spec was brought in line with the design (FR-004, FR-008, FR-017, US1-1; finding T1). **Cover letter:** the README deliverable is defined below. |

**Technology & Architecture Constraints**: .NET 10 with warnings treated as errors and Central Package Management ✅. Clean Architecture layering ✅. REST with `/api/v1`, ETag and Swagger ✅. In-memory by default ✅. Licensing: MediatR is pinned to 12.5.0 and Shouldly is used ✅. Serilog and health checks ✅.

**Result: PASS**, with no unjustified violations. The deliberate additions (two providers, the separate Identity host, two read technologies, and serialized writes on SQLite) are justified under Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-phonebook-management/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0: decisions R-01…R-16
├── data-model.md        # Phase 1: aggregate, VOs, events, schema, read models, messages
├── quickstart.md        # Phase 1: run & validation guide
├── contracts/
│   ├── phonebook-api.openapi.yaml
│   └── identity-token.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks – not created here)
```

### Source Code (repository root)

```text
PhoneBook.slnx
Directory.Build.props            # net10.0, Nullable, TreatWarningsAsErrors, analyzers
Directory.Packages.props         # Central Package Management
.editorconfig
docker-compose.yml               # api, identity, optional 'postgres' profile
README.md                        # ← cover letter (architecture, tech, process, AI + SDD)

src/
├── PhoneBook.SharedKernel/      # Result, Result<T>, Error, ErrorType, IResultBase,
│                                # Entity, AggregateRoot, IDomainEvent, IDateTimeProvider
├── PhoneBook.Domain/
│   └── Contacts/                # Contact (AR), ContactId, PersonName, PhoneNumber, Tag,
│       ├── Events/              # ContactCreated/Updated/TagChanged/Deleted DomainEvent
│       ├── Services/            # ContactDuplicateChecker, IContactUniquenessReader (port)
│       ├── ContactErrors.cs
│       └── IContactRepository.cs
├── PhoneBook.Application/
│   ├── Abstractions/
│   │   ├── Messaging/           # ICommand, ICommand<T>, IQuery<T>, handlers
│   │   ├── Behaviors/           # UnhandledException, Logging, Validation, UnitOfWork
│   │   ├── Data/                # IUnitOfWork, IReadDbContext, ISqlConnectionFactory
│   │   └── Events/              # DomainEventNotification<T>
│   ├── Contacts/
│   │   ├── Create/  Update/  Delete/            # command + validator + handler
│   │   ├── GetById/ (ReadDbContext)  GetByTag/ (Dapper)
│   │   ├── EventHandlers/       # audit logging of domain events
│   │   └── ContactResponse.cs
│   └── DependencyInjection.cs
├── PhoneBook.Infrastructure/
│   ├── Persistence/
│   │   ├── WriteDbContext.cs    # IUnitOfWork, event dispatch after commit, concurrency → Result
│   │   ├── ReadDbContext.cs     # no-tracking, read models, SaveChanges blocked
│   │   ├── Configurations/      # Contact (write) + ContactReadModel (read) mappings
│   │   ├── Repositories/        # ContactRepository, ContactUniquenessReader
│   │   ├── SqlConnectionFactory.cs   # Dapper
│   │   ├── DatabaseOptions.cs   # Provider: Sqlite | Postgres
│   │   └── InMemorySqliteKeepAlive.cs
│   ├── Time/SystemDateTimeProvider.cs
│   └── DependencyInjection.cs
├── PhoneBook.Api/
│   ├── Endpoints/Contacts/      # IEndpoint per operation (Create, GetById, GetByTag, Update, Delete)
│   ├── Infrastructure/          # GlobalExceptionHandler, ResultExtensions (Result→ProblemDetails),
│   │                            # ETag helpers, Swagger OAuth config, auth policies
│   ├── Program.cs               # public partial class Program (for WebApplicationFactory)
│   └── Dockerfile
└── PhoneBook.Identity/
    ├── Data/IdentityDbContext.cs       # OpenIddict EF Core stores (in-memory SQLite)
    ├── Seeding/ClientSeeder.cs         # IHostedService: phonebook-swagger, phonebook-readonly, scopes
    ├── Endpoints/TokenEndpoint.cs      # client_credentials passthrough handler
    ├── Program.cs
    └── Dockerfile

tests/
├── PhoneBook.Domain.UnitTests/         # VOs, Contact behaviour/events, ContactDuplicateChecker
├── PhoneBook.Api.IntegrationTests/
│   ├── Infrastructure/                 # PostgresContainerFixture (assembly fixture),
│   │                                   # PhoneBookApiFactory, Respawn reset, TestAuthHandler
│   ├── Commands/  Queries/             # via ISender inside a DI scope
│   └── Endpoints/                      # HTTP: status codes, ProblemDetails, ETag/If-Match, 401/403
├── PhoneBook.Identity.IntegrationTests/   # token endpoint, discovery, E2E token → API
└── PhoneBook.ArchitectureTests/           # layer dependency rules (NetArchTest)
```

**Structure Decision**: The solution uses Clean Architecture with four core layers (SharedKernel, Domain, Application, Infrastructure) plus two hosts (Api and Identity), and four test projects split by test kind. This matches how the user asked for tests to be divided: domain rules get unit tests, and commands, queries and the API get integration tests.

## Key Design Flows

**Command path** (for example, `POST /contacts`):

1. The endpoint turns the request into `CreateContactCommand` and calls `ISender.Send`.
2. The pipeline runs `UnhandledExceptionBehavior`, `LoggingBehavior`, `ValidationBehavior` (which returns a `Result` failure instead of throwing) and `UnitOfWorkBehavior`.
3. The handler builds the value objects (each returns a `Result`), then calls `ContactDuplicateChecker` (a domain service), then `Contact.Create` (which raises `ContactCreatedDomainEvent`), then `IContactRepository.Add`.
4. `UnitOfWorkBehavior` calls `WriteDbContext.SaveChangesAsync`, which commits and **then** publishes the domain events.
5. The endpoint maps the `Result` to HTTP: `201` with `Location` and `ETag`, or ProblemDetails with 400, 404, 409, 412 or 500.

**Query path**:
- `GetContactsByTagQuery` uses Dapper `SELECT … WHERE normalized_tag = @t` and returns `200 []` or `200` with the list.
- `GetContactByIdQuery` uses `ReadDbContext.Contacts` (no tracking) with a projection and returns `200` or `404`.

**Error contract**: every API error is ProblemDetails with an `errorCode`. The status-code mapping is in data-model §1.4, and the scope of the rule is in R-17.

**Exception safety**: any exception inside the pipeline becomes `Error.Unexpected`, then `500` ProblemDetails. Anything outside the pipeline is caught by `GlobalExceptionHandler`, which also returns `500` ProblemDetails with a `traceId`.

## Deliverable: README.md (cover letter)

The README lives at the repository root and is written in English, with the Persian brief quoted. Its sections:

1. Cover note to the reviewer
2. The brief, and how each bullet maps to an endpoint
3. Architecture: a layer diagram and the CQRS flow as Mermaid diagrams
4. DDD model
5. Technology table with the reason for each choice
6. Error-handling strategy: Result pattern and the two safety nets
7. Security: OpenIddict, scopes and the flow
8. Testing strategy and how to run the tests
9. How to run: in-memory, Swagger, compose
10. **Development process**: Spec-Driven Development with GitHub Spec Kit (`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`), AI pair-programming with Claude Code, and how the AI output was reviewed and verified by tests
11. Trade-offs and future work: outbox, projections, persistent store, auth code + PKCE, rate limiting, OpenTelemetry

## Spec Updates Made During Planning

- **FR-018 was added**, with the edge case updated to match: the same phone number cannot be registered twice under the same tag, and different tags remain allowed. This rule gives the domain service a real reason to exist.
- **The authentication assumption was replaced**: the API is now protected by OAuth2 client-credentials tokens from a separate identity app, as the user asked in the planning input.

### Revision 3 (2026-09-25, constitution v1.0.1)

- Re-ran the constitution check against v1.0.1. The only change there is the Principle III scope clarification, and the design already complied, so the result is still **PASS**.
- Clarified that the real OpenIddict 401 challenge must produce ProblemDetails (analysis U6, handled in tasks rev. 4).
- Wording fixes V1 and W3 from the fourth `/speckit-analyze` run.

### Revision 2 (2026-09-25, after `/speckit-analyze` and constitution v1.0.0)

- **The constitution check was re-run** against Principles I–VII (the section above).
- **spec.md was brought in line with the design** (T1):
  - FR-004: phone numbers are stored and returned in normalized form
  - FR-008: results are ordered by last name then first name, by character code
  - FR-017: an outdated edit is rejected as a failed precondition or a conflict
  - US1-1: the expected response shows the phone number in normalized form
- **Design additions:**
  - **Tag-search ordering:** done in code with ordinal comparison (P1)
  - **SQLite writes:** serialized through a write gate (P2)
  - **Swagger login:** the Identity host has a CORS policy and a fixed issuer, and the API has a `PublicAuthority` setting (U1, U5)
  - **401/403 responses:** ProblemDetails with an `errorCode` (Principle III)
  - **Malformed-id fallback:** requires authentication (K3)
  - **Scope of the ProblemDetails rule:** defined in R-17 (A2)

## Complexity Tracking

| Violation / added complexity | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Two database providers (in-memory SQLite and PostgreSQL) | The brief asks for in-memory storage, while Dapper, Testcontainers and Respawn need a real SQL engine that Respawn supports | EF InMemory cannot run SQL. PostgreSQL alone would break "in-memory" and force Docker just to run the app. |
| Separate Identity host (a 2nd deployable) | Explicit user requirement, and it shows OAuth2/OIDC skills | Local JWT signing inside the API would not be a "separate identity app" |
| CQRS with two read technologies (ReadDbContext and Dapper) | Explicit user requirement to showcase both | A single EF read path would not show Dapper |
| Pipeline `UnitOfWorkBehavior` plus domain events after commit | Shows the Unit of Work and event patterns correctly | Calling `SaveChanges` in each handler duplicates code and makes event timing easy to get wrong |
| Process-wide write gate (`SemaphoreSlim(1)`) around `SaveChangesAsync`, SQLite provider only | Concurrent writers on a shared-cache in-memory SQLite database can hit `SQLITE_LOCKED` and cause 500s, which would break FR-017 and SC-005 | Relying on the driver's lock retries is non-deterministic under load. PostgreSQL does not need the gate, so it is registered only for `Provider=Sqlite`. |
| Architecture test project (beyond the tests the user listed) | Enforces the Clean Architecture dependency rule automatically. It is cheap and is not a unit test of handlers, so it respects "unit tests only for domain". | Manual review cannot catch layer leaks as reliably. The project can be removed if the user prefers. |
