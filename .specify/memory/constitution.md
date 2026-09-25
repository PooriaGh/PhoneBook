<!--
Sync Impact Report
==================
Version change: 1.0.1 → 1.0.2
Bump rationale: PATCH. This amendment resolves /speckit-analyze findings C1 and X1 for feature 002. No
principle is added, removed or redefined:
  - C1: the commit-attribution rule changes form, not intent. AI assistance is still disclosed, now
    in the README cover letter (already required by Principle VII) instead of a per-commit trailer.
    This follows the project owner's standing instruction not to add Co-Authored-By trailers.
  - X1: Principle VI states explicitly that the identity application's OAuth protocol and sign-in
    endpoints are anonymous by design. The design already assumed this (feature 001 /connect/token;
    feature 002 /connect/authorize and /account/login). No obligation on business endpoints is weakened.

Modified principles:
  VI. Secure by Default: the anonymous-access bullet names the identity application's protocol and
      sign-in endpoints, and requires rate limiting on credential-accepting endpoints.
Modified sections:
  Development Workflow & Quality Gates, item 6 (Commits): the Co-Authored-By trailer requirement is
  replaced by README disclosure of AI assistance, and trailers are not added.
Added sections: none
Removed sections: none

Dependent artifacts (read at runtime, NOT modified by this command):
  ✅ .specify/templates/*: no conflict
  ✅ specs/001-phonebook-management/*: commits already had no trailer, and the README already
     discloses AI and SDD
  ✅ specs/002-production-readiness/plan.md: the Principle VI row already lists the login and
     authorize endpoints as the only new anonymous endpoints
  ✅ specs/002-production-readiness/tasks.md: the Notes line "without a Co-Authored-By trailer" now
     complies. T034 and T066 already rate-limit the token and login endpoints.

Deferred TODOs: none
-->

# PhoneBook Constitution

## Core Principles

### I. Domain-Driven Design with a Rich Domain Model

- Business rules MUST live in the Domain layer, inside aggregates, value objects or domain services.
  They MUST NOT live in endpoints, handlers or persistence code.
- Each aggregate root is a consistency boundary. Commands MUST modify at most one aggregate instance
  per transaction.
- Primitive business concepts (identifiers, phone numbers, tags, names) MUST be modelled as
  immutable value objects. Each one is created through a factory that validates its invariants.
- State changes that matter to the business MUST raise domain events. Events MUST be dispatched
  only after the transaction that produced them has committed successfully.
- A domain service MUST be introduced only for a rule that no single aggregate can enforce. Its
  data access MUST go through a port (an interface) owned by the Domain.
- The Domain and SharedKernel layers MUST NOT reference MediatR, EF Core, ASP.NET Core or any
  infrastructure package.

**Rationale**: The brief requires DDD. Keeping the model pure makes the rules testable without
infrastructure, and makes the architecture visible to reviewers.

### II. Result Pattern — No Exceptions for Control Flow (NON-NEGOTIABLE)

- Expected failures MUST be returned as `Result`/`Result<T>` carrying a typed `Error` with a code,
  a description and an `ErrorType`. MUST NOT throw for them. Expected failures include validation
  failures, not-found, conflicts and precondition failures.
- Value-object factories, aggregate methods, domain services, validators, pipeline behaviours and
  handlers MUST NOT throw for any business or input condition.
- Validation failures MUST identify each invalid field with its code and message, so that the API
  can return per-field errors.
- Infrastructure exceptions that correspond to business outcomes MUST be translated into `Result`
  failures at the persistence boundary. Examples are a concurrency conflict or a unique-key
  violation.
- `try/catch` blocks are permitted ONLY in these places:
  - the MediatR unhandled-exception behaviour
  - the global exception handler
  - the persistence-to-`Result` translation
  - post-commit domain-event dispatch (log and continue)

**Rationale**: Explicit results make failure paths visible in method signatures. They are cheaper
than exceptions and map to HTTP deterministically.

### III. Two Safety Nets for Unexpected Failures

- A MediatR pipeline behaviour MUST catch any exception escaping a request handler, log it with
  the request name, and return an `Unexpected` failure `Result`.
- A global ASP.NET Core exception handler (`IExceptionHandler`) MUST convert any remaining
  exception into an RFC 9457 `500` ProblemDetails that includes a `traceId`.
- Responses MUST NOT expose stack traces or exception messages outside the Development environment.
- Every error response from the API host's business routes, and every framework-generated
  error status on that host, MUST be ProblemDetails (validation errors use
  `ValidationProblemDetails`) with an `errorCode` extension. Framework-generated statuses include
  400, 401, 403, 404, 405, 409, 412 and 500, including challenges raised by the token-validation
  middleware.
- Exempt, because they follow their own standards:
  - health-check endpoints (`/health/*`), which return plain health-check output for orchestrators
  - OAuth 2.0 / OpenID Connect protocol endpoints of the identity application, which return
    RFC 6749 error JSON

**Rationale**: Consumers get a consistent error contract even when something unexpected happens.
Protocol and infrastructure endpoints keep the formats their own clients (OAuth libraries,
orchestrators) expect.

### IV. CQRS with Explicit Write and Read Paths

- Commands and queries MUST be separate MediatR messages (`ICommand`/`ICommand<T>` vs `IQuery<T>`),
  each with its own handler.
- The write side MUST access aggregates only through repositories and MUST commit only through the
  Unit of Work. Handlers MUST NOT call `SaveChanges` directly.
- The read side MUST NOT load aggregates or write data. It MUST use either the no-tracking,
  read-only `ReadDbContext` with read models, or Dapper through an `ISqlConnectionFactory`.
- Raw SQL MUST be parameterised, and it MUST run unchanged on every supported database provider.

**Rationale**: This separates rich invariant enforcement from lean, fast projections, which is the
central architectural showcase of this project.

### V. Test Discipline (NON-NEGOTIABLE)

- Unit tests MUST target only domain business rules: value objects, aggregate behaviour and events,
  and domain services. Handlers, endpoints and infrastructure MUST NOT be unit-tested with mocks.
- Commands, queries and HTTP endpoints MUST be covered by integration tests that use
  `WebApplicationFactory` against a real database:
  - Testcontainers provides the database
  - Respawn resets it between tests
- Every database provider the application can run on MUST be exercised by at least a smoke suite
  of integration tests.
- Every acceptance scenario in a feature spec MUST map to at least one automated test.
- Tests for a user story MUST be written before its implementation and MUST fail first.
- Layer dependency rules MUST be enforced by automated architecture tests.
- The full test suite MUST pass before a task, phase or feature is declared complete.

**Rationale**: Tests prove the requirements, not the mocks. Real-database integration tests catch
SQL, mapping and concurrency defects that unit tests cannot.

### VI. Secure by Default

- Every business endpoint MUST require an authorization policy. Anonymous access is limited to:
  - health checks and API documentation
  - the identity application's OAuth 2.0 / OpenID Connect protocol endpoints (for example token,
    authorize and discovery) and its sign-in pages, which are anonymous by design
- Endpoints that accept credentials MUST be rate-limited per source address. These are the token
  endpoint and the sign-in form.
- Tokens MUST be issued by the separate identity application built with OpenIddict and validated
  by the API. Scopes MUST distinguish read access from write access.
- Secrets MUST NOT be committed except clearly marked development-only client secrets. Production
  configuration MUST come from the environment.
- Concurrent modifications MUST be detected (optimistic concurrency) and MUST NOT be applied
  silently.

**Rationale**: Security and data integrity are baseline professional expectations, even for a
demo with in-memory storage.

### VII. Spec-Driven, AI-Assisted, Documented Delivery

- Every feature MUST follow the Spec Kit flow:
  specify → (clarify) → plan → tasks → analyze → implement.
- The artifacts under `specs/` are the source of truth. Code that diverges from them MUST be
  reconciled by updating the spec or the code, never by ignoring the difference.
- AI-generated output MUST be reviewed by a human and verified by tests before it is accepted.
  Significant human decisions MUST be recorded in `research.md` or in the README.
- The root `README.md` MUST serve as the cover letter. It MUST describe:
  - the architecture
  - the technologies and why each was chosen
  - the testing strategy
  - how to run the project
  - the development process, including Spec-Driven Development and AI assistance

**Rationale**: The brief requires a cover letter describing the architecture and the process. A
traceable spec-to-code flow is itself part of what is being demonstrated.

## Technology & Architecture Constraints

- **Platform**: .NET 10 (LTS) and C#. `Nullable` enabled, `TreatWarningsAsErrors` on, and
  Central Package Management through `Directory.Packages.props`.
- **Layering**: Clean Architecture with the dependency direction
  `SharedKernel ← Domain ← Application ← Infrastructure ← Api`. `Identity` is an independent host.
- **API**: RESTful, resource-oriented endpoints using the correct HTTP methods and status codes,
  URL-segment versioning (`/api/v{n}`), ETag and `If-Match` for concurrency, and documentation via
  Swagger.
- **Storage**: In-memory by default. Nothing is required to persist across restarts. Additional
  providers are allowed only behind configuration and MUST obey Principle V.
- **Licensing**: Dependencies MUST be usable without a commercial license. When a library changes
  its license, the last permissive version MUST be pinned and the choice documented. Examples:
  MediatR 12.5.0; Shouldly instead of FluentAssertions 8.
- **Observability**: Structured logging with Serilog, liveness and readiness health checks, and
  request logging.
- **Simplicity**: Any pattern beyond these principles MUST be justified in the plan's Complexity
  Tracking table.

## Development Workflow & Quality Gates

1. **Spec gate**: The spec has no `[NEEDS CLARIFICATION]` markers and its quality checklist passes.
2. **Plan gate**: The Constitution Check passes against Principles I–VII. Any deviation is recorded
   in Complexity Tracking.
3. **Analyze gate**: `/speckit-analyze` reports **zero CRITICAL** findings before `/speckit-implement`
   starts. HIGH findings MUST be resolved or explicitly accepted with a written reason.
4. **Implementation gate** (per user-story phase):
   - the story's tests were written first and failed first
   - the build has 0 warnings
   - all tests are green
5. **Delivery gate**:
   - the full solution builds and passes all tests in Release
   - the quickstart scenarios are validated manually
   - the README cover letter is complete
6. **Commits**: Small commits focused on single tasks. AI assistance is disclosed in the README
   cover letter (Principle VII), not in commit trailers. Commits MUST NOT carry a
   `Co-Authored-By` trailer for the AI assistant (project owner's decision).

## Governance

- This constitution overrides all other practices and guidance in this repository. Where a spec,
  plan or task conflicts with it, the artifact is changed, not the principle.
- **Amendments** are made only through `/speckit-constitution`. Each amendment MUST include a Sync
  Impact Report and a version bump, and dependent artifacts MUST be re-checked.
- **Versioning** follows semantic versioning:
  - MAJOR: a principle is removed or redefined incompatibly
  - MINOR: a principle or section is added, or guidance is materially expanded
  - PATCH: a clarification or wording change
- **Compliance**: Every plan runs the Constitution Check. Every `/speckit-analyze` run treats a
  constitution violation as CRITICAL. Reviews verify Principles II, V and VI explicitly.

**Version**: 1.0.2 | **Ratified**: 2026-09-25 | **Last Amended**: 2026-09-25
