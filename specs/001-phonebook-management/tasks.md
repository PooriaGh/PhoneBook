---

description: "Task list for Phone Book Management (001-phonebook-management)"
---

# Tasks: Phone Book Management

**Input**: Design documents from `/specs/001-phonebook-management/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: The user **explicitly requested** tests (`/speckit-plan` input):
- **Unit tests only for domain business rules**, in `tests/PhoneBook.Domain.UnitTests`.
- **Integration tests** for commands, queries and API endpoints, using WebApplicationFactory, Testcontainers (PostgreSQL) and Respawn, in `tests/PhoneBook.Api.IntegrationTests`.
- Identity integration tests, in `tests/PhoneBook.Identity.IntegrationTests`.
- Test tasks come **before** implementation in each story (write them, see them fail, then implement).

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md), so each story can be built and tested on its own.

**Revision 2 (2026-09-25)**: This version incorporates every `/speckit-analyze` finding (U1–U5, I1–I5, C1–C3, A1, D1) and the constitution (`.specify/memory/constitution.md` v1.0.0). Finding references appear in parentheses, for example *(U1)*.

**Revision 3 (2026-09-25)**: This version resolves the second `/speckit-analyze` run (K1, K2, K3, P1, P2, T1, E1, A2, S1, D2, L1) and matches the re-planned design (plan.md revision 2, research R-17).

**Revision 4 (2026-09-25)**: This version resolves the third `/speckit-analyze` run (U6, W1, W2, C4). A3 (the scope of constitution Principle III) needs a constitution PATCH through `/speckit-constitution` and is out of scope for this file. There are no new tasks and no renumbering.

**Revision 5 (2026-09-25)**: This version is aligned with constitution **v1.0.1** and plan.md revision 3. It resolves the fourth `/speckit-analyze` run (V1, U7, U8). There are no new tasks and no renumbering.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 = Add contact, US2 = Find by tag, US3 = Edit contact, US4 = Delete contact
- Paths are relative to the repository root (`D:\Programming\Projects\Interview Tasks\PhoneBook`)

## Global conventions (apply to every task)

- Target `net10.0`. Nullable is enabled, and warnings are errors, so fix warnings instead of suppressing them.
- **Never `throw` for expected failures**. Domain and application code returns `Result` / `Result<T>` (research R-05).
- Namespaces follow folder paths (for example `PhoneBook.Domain.Contacts`).
- Use `sealed` classes and records by default.
- Use file-scoped namespaces.
- One public type per file.
- Tests use **xUnit v3** and **Shouldly**. Name them `Method_Scenario_ExpectedResult`.
- MediatR is pinned to **12.5.0** (research R-08). Do not upgrade it.
- **Constitution v1.0.1 applies to every task.** Key points:
  - `try/catch` is allowed ONLY in `UnhandledExceptionBehavior`, `GlobalExceptionHandler`, the persistence-to-`Result` translation in `WriteDbContext`, and `DomainEventDispatcher` (Principle II).
  - Every error response from the API host's business routes and framework statuses is ProblemDetails with an `errorCode` extension. `/health/*` and the OAuth endpoints are exempt (Principle III as amended in v1.0.1).
  - Every database provider has integration tests (Principle V).
- Value-object validation failures are **always** returned as `ValidationError.For(field, code, description)`, never as a bare `Error` *(U2)*.
- Web hosts are referenced from tests through the marker classes `ApiAssemblyMarker` and `IdentityAssemblyMarker`, never through `Program`. Both hosts define a global `Program` *(I1)*.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution skeleton, central build settings and all projects, with the correct references.

- [X] T001 Create `global.json` at the repository root pinning SDK `10.0.400` with `"rollForward": "latestFeature"`.
- [X] T002 Create `Directory.Build.props` at the repository root with these properties:
  - `TargetFramework=net10.0`
  - `LangVersion=latest`
  - `Nullable=enable`
  - `ImplicitUsings=enable`
  - `TreatWarningsAsErrors=true`
  - `AnalysisLevel=latest-recommended`
  - `EnforceCodeStyleInBuild=true`
  - `ManagePackageVersionsCentrally=true`
- [X] T003 Create `Directory.Packages.props` at the repository root with `PackageVersion` entries, using the latest stable versions that are compatible with .NET 10 **except where pinned**:
  - Mediator and validation: `MediatR` **12.5.0**, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`
  - EF Core and data access: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Dapper`, `Microsoft.Data.Sqlite`, `Npgsql`
  - OpenIddict: `OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`, `OpenIddict.Validation.AspNetCore`, `OpenIddict.Validation.SystemNetHttp`
  - API: `Asp.Versioning.Http`, `Asp.Versioning.Mvc.ApiExplorer`, `Swashbuckle.AspNetCore`, `Serilog.AspNetCore`
  - Health checks: `AspNetCore.HealthChecks.NpgSql`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
  - Testing: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `NSubstitute`, `Bogus`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Respawn`, `NetArchTest.Rules`, `coverlet.collector`
- [X] T004 [P] Create `.editorconfig` (C# conventions: file-scoped namespaces, `var` when the type is apparent, `_camelCase` private fields, analyzer severities), `.gitignore` (the standard `dotnet new gitignore`) and `.dockerignore` (`bin/`, `obj/`, `tests/`, `.vs/`, `specs/`) at the repository root.
- [X] T005 Create the source projects and add them to `PhoneBook.slnx` under the `/src/` solution folder:
  - `dotnet new classlib`: `src/PhoneBook.SharedKernel`, `src/PhoneBook.Domain`, `src/PhoneBook.Application`, `src/PhoneBook.Infrastructure`
  - `dotnet new web`: `src/PhoneBook.Api`, `src/PhoneBook.Identity`
  - Delete the template `Class1.cs` files.
- [X] T006 Create the test projects (xUnit v3) and add them to `PhoneBook.slnx` under `/tests/`: `tests/PhoneBook.Domain.UnitTests`, `tests/PhoneBook.Api.IntegrationTests`, `tests/PhoneBook.Identity.IntegrationTests`, `tests/PhoneBook.ArchitectureTests`.
- [X] T007 Add project references, following the dependency rule (plan.md "Structure Decision"):
  - Source projects:
    - Domain → SharedKernel
    - Application → Domain
    - Infrastructure → Application
    - Api → Application and Infrastructure
    - Identity → no project references
  - Test projects:
    - Domain.UnitTests → Domain
    - Api.IntegrationTests → Api
    - Identity.IntegrationTests → Identity and Api
    - ArchitectureTests → Api
- [X] T008 Add `PackageReference` entries (no versions, because versions are managed centrally):
  - `SharedKernel`: none
  - `Domain`: none
  - `Application`: MediatR, FluentValidation, FluentValidation.DependencyInjectionExtensions, `Microsoft.Extensions.Logging.Abstractions`
  - `Infrastructure`: the EF Core packages, Sqlite, Npgsql, EFCore.NamingConventions, Dapper, the EF health checks
  - `Api`: Asp.Versioning.*, Swashbuckle.AspNetCore, Serilog.AspNetCore, OpenIddict.Validation.AspNetCore, OpenIddict.Validation.SystemNetHttp
  - `Identity`: OpenIddict.AspNetCore, OpenIddict.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite, Serilog.AspNetCore
  - Test projects: xunit.v3 and the other test packages from T003, as each project needs them
- [X] T009 Run `dotnet build PhoneBook.slnx` and confirm it succeeds with 0 warnings.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared kernel, the core domain model, the CQRS plumbing, persistence, the API host skeleton and the test harness. Every user story needs these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### SharedKernel (research R-05)

- [X] T010 [P] Create `src/PhoneBook.SharedKernel/Results/ErrorType.cs` (enum: `None`, `Validation`, `NotFound`, `Conflict`, `PreconditionFailed`, `Unexpected`) and `src/PhoneBook.SharedKernel/Results/Error.cs`:
  - `public record Error(string Code, string Description, ErrorType Type)`
  - static `None`
  - factories `Validation(code, description)`, `NotFound(...)`, `Conflict(...)`, `PreconditionFailed(...)`, `Unexpected(...)`
- [X] T011 [P] Create `src/PhoneBook.SharedKernel/Results/ValidationError.cs`:
  - `public sealed record FieldError(string Field, string Code, string Description)`
  - `public sealed record ValidationError(IReadOnlyList<FieldError> Errors) : Error("General.Validation", "One or more validation errors occurred.", ErrorType.Validation)`
  - `public static ValidationError For(string field, string code, string description)`, which creates a single-field error *(U2)*
- [X] T012 Create `src/PhoneBook.SharedKernel/Results/IResultBase.cs`, `IResultFactory.cs`, `Result.cs` and `ResultOfT.cs`:
  - `interface IResultBase { bool IsSuccess { get; } bool IsFailure { get; } Error Error { get; } }`
  - `interface IResultFactory<TSelf> where TSelf : IResultFactory<TSelf> { static abstract TSelf Failure(Error error); }`
  - `class Result : IResultBase, IResultFactory<Result>`:
    - `Success()`, `Failure(Error)`
    - `Success<T>(T)`
    - `Combine(params Result[])`: if every failure is a `ValidationError`, flattens all their `FieldError`s into **one** `ValidationError`; otherwise returns the first non-validation failure *(U2)*
    - the constructor guards invalid states *without throwing*, by using `Debug.Assert`
  - `class Result<T> : Result, IResultFactory<Result<T>>`:
    - `Value`: reading it on a failure returns `default!`; document this in XML docs
    - `static new Failure(Error)`
    - implicit conversions from `T` and from `Error`
    - `Match<TOut>(Func<T,TOut>, Func<Error,TOut>)`
- [X] T013 [P] Create `src/PhoneBook.SharedKernel/Domain/IDomainEvent.cs` (`interface IDomainEvent { Guid EventId { get; } DateTime OccurredOnUtc { get; } }`) and `src/PhoneBook.SharedKernel/Domain/DomainEvent.cs` (an abstract record base that sets `EventId = Guid.CreateVersion7()` and `OccurredOnUtc = DateTime.UtcNow`).
- [X] T014 [P] Create `src/PhoneBook.SharedKernel/Domain/Entity.cs`:
  - `abstract class Entity<TId> where TId : notnull`
  - `Id` with a protected setter
  - equality by Id
- [X] T015 [P] Create `src/PhoneBook.SharedKernel/Domain/AggregateRoot.cs`:
  - `abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents`
  - a private `List<IDomainEvent>`
  - `protected void Raise(IDomainEvent)`
  - `IReadOnlyCollection<IDomainEvent> DomainEvents`
  - `void ClearDomainEvents()`
  - Also create `IHasDomainEvents.cs` in the same folder.
- [X] T016 [P] Create `src/PhoneBook.SharedKernel/Time/IDateTimeProvider.cs` (`DateTime UtcNow { get; }`).

### Domain unit tests: write FIRST (constitution Principle V, finding K1)

These tests cover the value objects, `Contact.Create` and the duplicate checker. Those are implemented in this phase, so their tests must be written **before** the code (T023–T031) and must be seen to fail.

- [X] T017 [P] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/PersonNameTests.cs`:
  - valid Persian names (including ZWNJ `"محمد\u200Cرضا"`) are trimmed and kept
  - a blank or null first name gives `PersonName.FirstName.Required`
  - a 101-character last name gives `PersonName.LastName.TooLong`
  - exactly 100 characters is OK
  - both parts invalid gives one `ValidationError` containing 2 `FieldError`s
- [X] T018 [P] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/PhoneNumberTests.cs` (`[Theory]`):
  - `"+98 912-123-4567"` becomes `"+989121234567"`
  - Persian `"۰۹۱۲۱۲۳۴۵۶۷"` becomes `"09121234567"`
  - Arabic-Indic `"٠٩١٢"` becomes `"0912"`
  - `"abc"` and `"12+34"` give `PhoneNumber.InvalidCharacters`
  - `"123"` and a 16-digit number give `PhoneNumber.InvalidLength`
  - `""`/whitespace/null gives `PhoneNumber.Required`
  - 4 and 15 digits are OK
- [X] T019 [P] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/TagTests.cs`:
  - `" Work "` becomes Value `"Work"` and NormalizedValue `"WORK"`
  - `Tag("work") == Tag("WORK ")`
  - `"همکار" != "همکاران"`
  - blank gives `Tag.Required`
  - 51 characters gives `Tag.TooLong`
  - 50 characters is OK
- [X] T020 [P] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/ContactCreateTests.cs` (use a stub `IDateTimeProvider`): `Create` sets a non-empty Id, `Version == 1` and `CreatedAtUtc == clock.UtcNow`, and raises exactly one `ContactCreatedDomainEvent` with the matching Id, phone and tag.
- [X] T021 [P] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/ContactDuplicateCheckerTests.cs` (NSubstitute `IContactUniquenessReader`):
  - reader returns true → `Contact.Duplicate`
  - reader returns false → success
  - the `excluding` id is passed through
- [X] T022 Run `dotnet test tests/PhoneBook.Domain.UnitTests` and confirm it is **red**. Compile errors for the not-yet-existing domain types count as red. Note this in the commit message, for example `test: add failing domain tests (red)` *(K1)*.

### Domain core: Contact aggregate and value objects (data-model §1)

- [X] T023 [P] Create `src/PhoneBook.Domain/Contacts/ContactId.cs`:
  - `public readonly record struct ContactId(Guid Value)`
  - `static ContactId New() => new(Guid.CreateVersion7())`
  - `static Result<ContactId> From(Guid value)`, which returns `ContactErrors.IdEmpty` (code `Contact.Id.Empty`, Validation) when the value is `Guid.Empty`
  - `ToString()` returns `Value.ToString()`
- [X] T024 [P] Create `src/PhoneBook.Domain/Contacts/PersonName.cs`:
  - `public sealed record PersonName` with `FirstName` and `LastName`, a private constructor and `const int MaxLength = 100`
  - `static Result<PersonName> Create(string? firstName, string? lastName)`
  - **Invariants (verbatim): "Each part is trimmed, not blank and at most 100 characters. Any Unicode is allowed, including the ZWNJ U+200C."**
  - Collect *both* field errors through `Result.Combine`, using field names `firstName`/`lastName` and the codes:
    - `PersonName.FirstName.Required`
    - `PersonName.FirstName.TooLong`
    - `PersonName.LastName.Required`
    - `PersonName.LastName.TooLong`
- [X] T025 [P] Create `src/PhoneBook.Domain/Contacts/PhoneNumber.cs`:
  - `public sealed record PhoneNumber` with `Value` and a private constructor
  - `const int MinDigits = 4`, `MaxDigits = 15`
  - `static Result<PhoneNumber> Create(string? raw)`
  - **Rule (verbatim): "Persian and Arabic-Indic digits are converted to ASCII. Then: an optional leading `+`, then only digits, spaces and `-`. After spaces and hyphens are removed, 4–15 digits remain. The stored value is `+?digits`."**
    - Map U+06F0–U+06F9 and U+0660–U+0669 to `'0'`–`'9'`.
    - Trim the input.
    - Blank input → `PhoneNumber.Required`.
    - Any character other than a digit, space or `-` (with `+` allowed only at index 0) → `PhoneNumber.InvalidCharacters`.
    - A digit count outside 4–15 → `PhoneNumber.InvalidLength`.
    - All three errors use field `phoneNumber`.
    - Do not use a Regex in a way that could throw. A compiled `[GeneratedRegex]` is allowed.
- [X] T026 [P] Create `src/PhoneBook.Domain/Contacts/Tag.cs`:
  - `public sealed class Tag : IEquatable<Tag>` with `Value` (the display value) and `NormalizedValue`
  - `const int MaxLength = 50`
  - `static Result<Tag> Create(string? raw)`
  - **Rule (verbatim): "Trimmed, not blank, at most 50 characters. `NormalizedValue = Value.ToUpperInvariant()`. Two tags are equal when their `NormalizedValue` matches."**
  - Error codes `Tag.Required` and `Tag.TooLong`, field `tag`
  - Equality and `GetHashCode` use `NormalizedValue` only.
  - Also add `static string Normalize(string raw) => raw.Trim().ToUpperInvariant()` for use by queries.
- [X] T027 [P] Create `src/PhoneBook.Domain/Contacts/ContactErrors.cs` with these static `Error`s:
  - `IdEmpty` (Validation)
  - `NotFound(ContactId)` → `Contact.NotFound` (NotFound)
  - `Duplicate` → `Contact.Duplicate` (Conflict), "A contact with this phone number already exists under this tag."
  - `ConcurrencyConflict` → `Contact.ConcurrencyConflict` (Conflict)
  - `VersionMismatch` → `Contact.VersionMismatch` (PreconditionFailed)
- [X] T028 [P] Create the domain events as sealed records deriving from `DomainEvent`:
  - `src/PhoneBook.Domain/Contacts/Events/ContactCreatedDomainEvent.cs`: `(ContactId ContactId, string PhoneNumber, string Tag)`
  - `ContactUpdatedDomainEvent.cs`: `(ContactId ContactId)`
  - `ContactTagChangedDomainEvent.cs`: `(ContactId ContactId, string OldTag, string NewTag)`
  - `ContactDeletedDomainEvent.cs`: `(ContactId ContactId, string Tag)`
- [X] T029 Create `src/PhoneBook.Domain/Contacts/Contact.cs`:
  - `public sealed class Contact : AggregateRoot<ContactId>`
  - properties `Name` (PersonName), `Phone` (PhoneNumber), `Tag` (Tag), `int Version`, `DateTime CreatedAtUtc`, `DateTime? UpdatedAtUtc`
  - a private parameterless constructor for EF
  - `static Result<Contact> Create(PersonName name, PhoneNumber phone, Tag tag, IDateTimeProvider clock)`: sets `Id = ContactId.New()`, `Version = 1` and `CreatedAtUtc = clock.UtcNow`, then raises `ContactCreatedDomainEvent`
  - Leave `Update` and `MarkAsDeleted` for US3 and US4. Depends on T023–T028.
- [X] T030 [P] Create `src/PhoneBook.Domain/Contacts/IContactRepository.cs`: `Task<Contact?> GetByIdAsync(ContactId id, CancellationToken ct)`, `void Add(Contact contact)`, `void Remove(Contact contact)`.
- [X] T031 Create the domain service:
  - `src/PhoneBook.Domain/Contacts/Services/IContactUniquenessReader.cs`: `Task<bool> ExistsAsync(PhoneNumber phone, Tag tag, ContactId? excluding, CancellationToken ct)`
  - `src/PhoneBook.Domain/Contacts/Services/ContactDuplicateChecker.cs`: `Task<Result> EnsureNotDuplicateAsync(PhoneNumber phone, Tag tag, ContactId? excluding, CancellationToken ct)`, which returns `ContactErrors.Duplicate` when `ExistsAsync` is true
  - **Rule (verbatim, FR-018): "the same phone number cannot be registered twice under the same tag".**

- [X] T032 Run `dotnet test PhoneBook.slnx` and confirm that all the domain unit tests from T017–T021 are now **green** *(K1, K2)*.

### Application: CQRS plumbing (research R-07, R-08, R-09)

- [X] T033 [P] Create the messaging abstractions in `src/PhoneBook.Application/Abstractions/Messaging/`:
  - `ICommand : IRequest<Result>, IBaseCommand`
  - `ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand`
  - `IBaseCommand` (a marker)
  - `IQuery<TResponse> : IRequest<Result<TResponse>>`
  - `ICommandHandler<TCommand>`, `ICommandHandler<TCommand,TResponse>`, `IQueryHandler<TQuery,TResponse>` (wrappers over `IRequestHandler`)
- [X] T034 [P] Create the data abstractions in `src/PhoneBook.Application/Abstractions/Data/`:
  - `IUnitOfWork` (`Task<Result> SaveChangesAsync(CancellationToken ct)`)
  - `IReadDbContext` (`IQueryable<ContactReadModel> Contacts { get; }`)
  - `ISqlConnectionFactory` (`DbConnection CreateConnection()`)
  - `ReadModels/ContactReadModel.cs` (a class with `Guid Id`, `string FirstName`, `string LastName`, `string PhoneNumber`, `string Tag`, `string NormalizedTag`, `int Version`, `DateTime CreatedAtUtc`, `DateTime? UpdatedAtUtc`)
- [X] T035 [P] Create `src/PhoneBook.Application/Contacts/ContactResponse.cs` (`public sealed record ContactResponse(Guid Id, string FirstName, string LastName, string PhoneNumber, string Tag, int Version)`) with the static mappers `FromDomain(Contact)` and `FromReadModel(ContactReadModel)`.
- [X] T036 [P] Create `src/PhoneBook.Application/Abstractions/Events/DomainEventNotification.cs`:
  - `public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification where TEvent : IDomainEvent`
  - Also create `IDomainEventDispatcher.cs` (`Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)`) and `DomainEventDispatcher.cs`, which wraps each event in `DomainEventNotification<>` using `Activator.CreateInstance(typeof(DomainEventNotification<>).MakeGenericType(e.GetType()), e)` and calls `IPublisher.Publish`. **Each publish is wrapped in its own try/catch that logs the error (with the event type and `EventId`) and continues.** The data is already committed, so a failing handler must never turn a successful command into a 500 *(U3; constitution Principle II allows this catch)*.
- [X] T037 [P] Create `src/PhoneBook.Application/Abstractions/Behaviors/UnhandledExceptionBehavior.cs`:
  - `IPipelineBehavior<TRequest,TResponse> where TRequest : notnull where TResponse : IResultBase, IResultFactory<TResponse>`
  - wraps `next()` in try/catch(Exception)
  - logs an error with the request type name
  - returns `TResponse.Failure(Error.Unexpected("General.Unexpected", "An unexpected error occurred."))`
  - Rethrow **only** `OperationCanceledException` when the token was cancelled. Add an XML comment saying this is the one documented exception to constitution Principle II: cancellation is not a business failure *(L1)*.
- [X] T038 [P] Create `src/PhoneBook.Application/Abstractions/Behaviors/LoggingBehavior.cs`: logs the request start and end with `{RequestName}` and the elapsed milliseconds (`Stopwatch`), logs a warning when the request takes more than 500 ms, and logs the error code on failure results.
- [X] T039 [P] Create `src/PhoneBook.Application/Abstractions/Behaviors/ValidationBehavior.cs`:
  - runs all `IValidator<TRequest>` in parallel
  - if any failure exists, **returns** `TResponse.Failure(new ValidationError(...))`, mapping `PropertyName` to a camelCase `Field`, `ErrorCode` to `Code` and `ErrorMessage` to `Description`
  - never throws
- [X] T040 [P] Create `src/PhoneBook.Application/Abstractions/Behaviors/UnitOfWorkBehavior.cs`:
  - applies only when `TRequest : IBaseCommand`
  - calls `next()`
  - if the result is a success, calls `IUnitOfWork.SaveChangesAsync`, and if that fails returns `TResponse.Failure(saveResult.Error)`
  - otherwise returns the handler result
- [X] T041 Create `src/PhoneBook.Application/DependencyInjection.cs` (`AddApplication(this IServiceCollection)`):
  - registers MediatR from the assembly
  - adds the open behaviors **in this order**: `UnhandledExceptionBehavior`, `LoggingBehavior`, `ValidationBehavior`, `UnitOfWorkBehavior`
  - registers `AddValidatorsFromAssembly(..., includeInternalTypes: true)`, `ContactDuplicateChecker` (scoped) and `IDomainEventDispatcher` (scoped)
  - Depends on T031, T036–T040.

### Infrastructure: persistence with the provider switch (research R-02, R-07, R-09, R-10)

- [X] T042 [P] Create `src/PhoneBook.Infrastructure/Persistence/DatabaseOptions.cs`:
  - section `Database`
  - `DatabaseProvider Provider` (enum `Sqlite`, `Postgres`)
  - `string ConnectionString` (default `Data Source=phonebook;Mode=Memory;Cache=Shared`)
  - a validation attribute that requires `ConnectionString`
- [X] T043 [P] Create `src/PhoneBook.Infrastructure/Persistence/InMemorySqliteKeepAlive.cs`: a singleton that opens a `SqliteConnection` to the configured connection string in its constructor and keeps it open until `Dispose`. This keeps the shared in-memory database alive (research R-02). In the same folder, create `SqliteWriteGate.cs`: a singleton wrapping `SemaphoreSlim(1, 1)` with `Task<IDisposable> AcquireAsync(CancellationToken ct)`. It serializes writes on the shared-cache in-memory database (research R-02, P2).
- [X] T044 [P] Create `src/PhoneBook.Infrastructure/Time/SystemDateTimeProvider.cs` (implements `IDateTimeProvider`).
- [X] T045 Create `src/PhoneBook.Infrastructure/Persistence/Configurations/ContactConfiguration.cs` (`IEntityTypeConfiguration<Contact>`), mapping the table `contacts` **exactly as in data-model §2**:
  - `id`: PK, converted from `ContactId`
  - `first_name` and `last_name`: max 100, required (`OwnsOne`/complex property for `PersonName`, with explicit column names)
  - `phone_number`: max 16, required (a converter for `PhoneNumber`, using a factory that bypasses validation with the stored value, e.g. an `internal static PhoneNumber FromPersisted(string)` exposed through `InternalsVisibleTo("PhoneBook.Infrastructure")` in Domain)
  - `tag` and `normalized_tag`: max 50, required, with index `ix_contacts_normalized_tag`
  - `version`: `.IsConcurrencyToken()`
  - `created_at_utc`, `updated_at_utc` (nullable)
  - unique index `ux_contacts_phone_tag (phone_number, normalized_tag)`
  - ignore `DomainEvents`
  - Add `[assembly: InternalsVisibleTo("PhoneBook.Infrastructure")]` in `src/PhoneBook.Domain/AssemblyInfo.cs`, and `FromPersisted` factories on `PersonName`, `PhoneNumber` and `Tag`.
- [X] T046 Create `src/PhoneBook.Infrastructure/Persistence/WriteDbContext.cs`:
  - `DbContext, IUnitOfWork`
  - `DbSet<Contact> Contacts`
  - applies `ContactConfiguration`
  - `Task<Result> IUnitOfWork.SaveChangesAsync(ct)` does the following:
    1. Collects `DomainEvents` from `ChangeTracker.Entries<IHasDomainEvents>()`.
    2. Clears the events.
    3. Calls `base.SaveChangesAsync`. If a `SqliteWriteGate` is registered (inject it as an optional `SqliteWriteGate?` via a constructor parameter), acquire it around the save with `using (await gate.AcquireAsync(ct))`. Use no gate for PostgreSQL *(P2)*.
    4. Catches `DbUpdateConcurrencyException` and returns `ContactErrors.ConcurrencyConflict`.
    5. Catches a `DbUpdateException` whose inner exception is a unique violation (SQLite error 19 / `SqliteException.SqliteExtendedErrorCode == 2067`, or Postgres `PostgresException.SqlState == "23505"`) and returns `ContactErrors.Duplicate`.
    6. **After** a successful commit, awaits `IDomainEventDispatcher.DispatchAsync(events)`, which never throws (see T036) *(U3)*.
    7. Returns `Result.Success()`.
  - Depends on T045.
- [X] T047 Create `src/PhoneBook.Infrastructure/Persistence/ReadDbContext.cs` and `Configurations/ContactReadModelConfiguration.cs`:
  - `ReadDbContext : DbContext, IReadDbContext`
  - `QueryTrackingBehavior.NoTracking` is set in the constructor
  - `ContactReadModel` is mapped to the **same** `contacts` table and columns
  - `SaveChanges`/`SaveChangesAsync` are overridden to return `0` *and* log a warning. The read context is read-only by design and must not throw (research R-07).
  - `IQueryable<ContactReadModel> Contacts => Set<ContactReadModel>()`
- [X] T048 [P] Create `src/PhoneBook.Infrastructure/Persistence/SqlConnectionFactory.cs` (`ISqlConnectionFactory`): returns a new `SqliteConnection` or `NpgsqlConnection`, depending on `DatabaseOptions.Provider`.
- [X] T049 [P] Create `src/PhoneBook.Infrastructure/Persistence/Repositories/ContactRepository.cs` (`IContactRepository` over `WriteDbContext`: `GetByIdAsync` uses `FirstOrDefaultAsync(c => c.Id == id)`, `Add`, `Remove`) and `ContactUniquenessReader.cs`:
  - `IContactUniquenessReader` using `WriteDbContext.Contacts.AnyAsync`
  - matches phone value **and** tag normalized value, and excludes `excluding`
  - also checks `ChangeTracker.Entries<Contact>()` with state `Added`, so that unsaved duplicates in the same unit of work are caught
- [X] T050 Create `src/PhoneBook.Infrastructure/DependencyInjection.cs` (`AddInfrastructure(this IServiceCollection, IConfiguration)`):
  - binds `DatabaseOptions` with `ValidateDataAnnotations().ValidateOnStart()`
  - for the provider `Sqlite`: registers `InMemorySqliteKeepAlive` (singleton), `SqliteWriteGate` (singleton, **Sqlite only**) *(P2)*, `UseSqlite`
  - for the provider `Postgres`: `UseNpgsql`
  - both contexts get `.UseSnakeCaseNamingConvention()`
  - registers `WriteDbContext`, `ReadDbContext`, the `IUnitOfWork` → `WriteDbContext` mapping, `IReadDbContext` → `ReadDbContext`, `ISqlConnectionFactory`, the repositories and `IDateTimeProvider` (singleton)
  - registers `DatabaseInitializer : IHostedService` (`src/PhoneBook.Infrastructure/Persistence/DatabaseInitializer.cs`), which resolves the keep-alive (for Sqlite) and calls `WriteDbContext.Database.EnsureCreatedAsync()`
  - adds a health check `AddDbContextCheck<WriteDbContext>("database", tags: ["ready"])`
  - Depends on T042–T049.

### API host skeleton (research R-08, R-11, R-12)

- [X] T051 [P] Create `src/PhoneBook.Api/Infrastructure/GlobalExceptionHandler.cs`:
  - `IExceptionHandler`
  - logs the exception
  - writes a `500` ProblemDetails through `IProblemDetailsService`, with `title = "Server error"`, `type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"`, and `extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier`
  - includes `detail = exception.Message` **only** when `IHostEnvironment.IsDevelopment()`
- [X] T052 [P] Create `src/PhoneBook.Api/Infrastructure/ResultExtensions.cs`:
  - `IResult ToProblem(this Result result)`, which maps `ErrorType`:
    - Validation → `400` `ValidationProblemDetails`, grouping `FieldError`s by `Field` with the message `"{Code}: {Description}"`; a non-`ValidationError` validation error goes under key `""`
    - NotFound → `404`
    - Conflict → `409`
    - PreconditionFailed → `412`
    - Unexpected/other → `500`
  - every problem carries `extensions["errorCode"] = error.Code` and `traceId`
- [X] T053 [P] Create `src/PhoneBook.Api/Infrastructure/ETagExtensions.cs`:
  - `static string ToETag(int version) => $"\"{version}\""`
  - `static Result<int?> TryParseIfMatch(HttpRequest request)`: missing header → `null`; `"*"` → `null`; `"n"` → `n`; malformed → a Validation error `Request.IfMatch.Invalid`
- [X] T054 [P] Create `src/PhoneBook.Api/Endpoints/IEndpoint.cs` (`void MapEndpoint(IEndpointRouteBuilder app)`) and `src/PhoneBook.Api/Endpoints/EndpointExtensions.cs`:
  - `AddEndpoints(Assembly)` scans non-abstract `IEndpoint` types and registers them as `IEndpoint` transient
  - `MapEndpoints(this WebApplication, RouteGroupBuilder? group)`
- [X] T055 [P] Create `src/PhoneBook.Api/Infrastructure/Auth/AuthOptions.cs` (section `Auth`: `Authority` (the token issuer, which must equal the Identity `Identity:Issuer`), `PublicAuthority` (the browser-reachable Identity base URL for Swagger; defaults to `Authority`) *(U5)*, `Audience = "phonebook-api"`, `bool RequireHttpsMetadata`), `Scopes.cs` (`Read = "phonebook.read"`, `Write = "phonebook.write"`) and `Policies.cs` (`ContactsRead = "Contacts.Read"`, `ContactsWrite = "Contacts.Write"`).
- [X] T056 Create `src/PhoneBook.Api/Infrastructure/Auth/AuthenticationSetup.cs` (`AddPhoneBookAuth(this IServiceCollection, IConfiguration)`):
  - OpenIddict validation with `SetIssuer(Authority)`, `AddAudiences(Audience)`, `UseSystemNetHttp()`, `UseAspNetCore()`
  - default scheme `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme`
  - authorization policies that each require the claim `scope` to contain the respective scope. Split the space-separated `scope` claim value; use `RequireAssertion`.
  - Depends on T055.
  - **Before writing this handler** *(U7)*: confirm the exact OpenIddict 7.x validation event API (`OpenIddictValidationEvents.ProcessChallengeContext`, the handler-descriptor ordering and `HandleRequest()`) against the **installed** package version, using Context7 (`/openiddict/openiddict-core`) or the OpenIddict docs. The type names below are guidance; use the equivalents the installed version actually exposes.
  - **ProblemDetails for real challenges** *(U6)*: register an OpenIddict validation event handler for `OpenIddictValidationEvents.ProcessChallengeContext`, and one for `ProcessErrorContext` (`options.AddEventHandler<...>(b => b.UseInlineHandler(...).SetOrder(OpenIddictValidationAspNetCoreHandlers.AttachHttpResponseCode<...>.Descriptor.Order + 1))`, or an equivalent `IOpenIddictValidationHandler<>` class in `src/PhoneBook.Api/Infrastructure/Auth/ProblemDetailsChallengeHandler.cs`). The handler:
    - writes a `401` ProblemDetails through `IProblemDetailsService`, with `errorCode = "Auth.Unauthorized"`, `traceId`, and `detail` = the OpenIddict `error_description` (Development only)
    - keeps the `WWW-Authenticate` header
    - calls `context.HandleRequest()`, so OpenIddict writes no competing body
    - uses no `throw` and no `catch`
    - `403` from the authorization middleware is already covered by `UseStatusCodePages` (empty body); the end-to-end test in the Identity phase verifies both
- [X] T057 [P] Create `src/PhoneBook.Api/Infrastructure/Swagger/SwaggerSetup.cs`:
  - Swashbuckle with one document per API version (v1)
  - an OAuth2 `clientCredentials` security scheme whose `tokenUrl` is `{Auth:PublicAuthority}/connect/token` *(U5)*, with scopes `phonebook.read` and `phonebook.write`
  - a security requirement on endpoints that have authorization metadata
  - UI `OAuthClientId("phonebook-swagger")`
- [X] T058 Create `src/PhoneBook.Api/Program.cs`:
  - Serilog (`UseSerilog`, reads configuration, console sink, request logging)
  - `AddProblemDetails(o => o.CustomizeProblemDetails = ctx => { ... })` and `AddExceptionHandler<GlobalExceptionHandler>()`. The customisation adds `traceId` to every problem. When no `errorCode` is set yet, it adds one based on the status code:
    - `401` → `Auth.Unauthorized`
    - `403` → `Auth.Forbidden`
    - `404` → `General.NotFound`
    - `405` → `General.MethodNotAllowed`
    - any other status → `General.Error`
  - Together with `UseStatusCodePages()`, this makes body-less `401`/`403` responses into ProblemDetails *(constitution Principle III)*
  - Scope (research R-17, A2): this applies to the API host only. `/health/*` keeps plain health-check output, and the Identity host keeps RFC 6749 OAuth errors.
  - `AddApplication()` and `AddInfrastructure(config)`
  - `AddPhoneBookAuth(config)`
  - `AddApiVersioning(o => o.DefaultApiVersion = new(1); o.ReportApiVersions = true; UrlSegmentApiVersionReader).AddApiExplorer(o => { GroupNameFormat = "'v'VVV"; SubstituteApiVersionInUrl = true; })`
  - `AddEndpoints(typeof(Program).Assembly)` and Swagger
  - `UseExceptionHandler()` and `UseStatusCodePages()`
  - `UseSerilogRequestLogging()`
  - `UseAuthentication()` and `UseAuthorization()`
  - a versioned group `api/v{version:apiVersion}` with `ApiVersionSet`, then `MapEndpoints(group)`
  - `MapHealthChecks("/health/live", Predicate = _ => false)` and `MapHealthChecks("/health/ready", tag "ready")`
  - Swagger UI enabled in Development
  - end the file with `public partial class Program;`
  - Also create `src/PhoneBook.Api/ApiAssemblyMarker.cs` (`public sealed class ApiAssemblyMarker;`, namespace `PhoneBook.Api`), for use by `WebApplicationFactory<ApiAssemblyMarker>` *(I1)*
  - Depends on T041, T050–T057.
- [X] T059 [P] Create `src/PhoneBook.Api/appsettings.json` (Serilog levels; `Database: { Provider: "Sqlite", ConnectionString: "Data Source=phonebook;Mode=Memory;Cache=Shared" }`; `Auth: { Authority: "https://localhost:7002/", PublicAuthority: "https://localhost:7002", Audience: "phonebook-api" }` *(U5)*), `appsettings.Development.json`, and `Properties/launchSettings.json` (profile `https` on `https://localhost:7001;http://localhost:5001`, launch URL `swagger`).
- [X] T060 Run `dotnet run --project src/PhoneBook.Api` and confirm `GET /health/live` returns 200 and `/swagger` loads. Then run `dotnet build` and confirm 0 warnings.

### Integration test harness (research R-14)

- [X] T061 [P] Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/PostgresContainerFixture.cs`:
  - `IAsyncLifetime`
  - `PostgreSqlBuilder().WithImage("postgres:17-alpine").Build()`
  - exposes `ConnectionString`
  - registered as an **xUnit v3 assembly fixture** via `[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]` in `tests/PhoneBook.Api.IntegrationTests/AssemblyInfo.cs`
- [X] T062 [P] Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/TestAuthHandler.cs`:
  - `AuthenticationHandler<AuthenticationSchemeOptions>`, scheme `"Test"`
  - reads the request header `X-Test-Scopes`:
    - missing → both scopes (`phonebook.read phonebook.write`)
    - the value `none` → `AuthenticateResult.NoResult()` (which leads to 401)
    - otherwise → that value becomes the `scope` claim
  - `sub = "test-client"`
- [X] T063 Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/PhoneBookApiFactory.cs` (`WebApplicationFactory<ApiAssemblyMarker>` *(I1)*). It takes `PostgresContainerFixture` and:
  - sets `Database:Provider=Postgres` and `Database:ConnectionString=fixture.ConnectionString` via `ConfigureAppConfiguration`/`UseSetting`
  - in `ConfigureTestServices`, sets the default authenticate/challenge scheme to `"Test"` and adds `TestAuthHandler`
  - exposes `ResetDatabaseAsync()`, which uses a lazily initialised `Respawner.CreateAsync(conn, new RespawnerOptions { DbAdapter = DbAdapter.Postgres, SchemasToInclude = ["public"] })` after the host is started (so the schema exists)
  - exposes `HttpClient CreateClientWithScopes(string? scopes)`
  - Depends on T061, T062.
- [X] T064 Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/BaseIntegrationTest.cs` (implements `IAsyncLifetime`):
  - `InitializeAsync` calls `factory.ResetDatabaseAsync()`
  - provides `ISender Sender` from a fresh `IServiceScope`
  - provides `HttpClient Client`
  - provides a `Task<Contact> SeedContactAsync(string first, string last, string phone, string tag)` helper that uses `WriteDbContext`/`IUnitOfWork` directly
  - Also create `Infrastructure/ContactFaker.cs` (Bogus: Persian-locale names `new Faker("fa")`, valid `+98912#######` numbers, word tags).
  - Share one factory per test collection via an xUnit v3 `[CollectionDefinition]` in `Infrastructure/IntegrationTestCollection.cs`.
- [X] T065 Create `tests/PhoneBook.Api.IntegrationTests/Infrastructure/SqliteApiFactory.cs` (`WebApplicationFactory<ApiAssemblyMarker>`) for the **default in-memory SQLite provider** *(C1, constitution Principle V)*:
  - sets `Database:Provider=Sqlite` and `Database:ConnectionString=Data Source=phonebook-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared`, so every factory instance gets its own isolated database
  - uses the same `TestAuthHandler` setup as T063
  - exposes `ResetDatabaseAsync()`, which runs `DELETE FROM contacts` through `ISqlConnectionFactory` and Dapper. Respawn does not support SQLite; document this in an XML comment.
  - exposes `CreateClientWithScopes(string? scopes)`
  - Add `Infrastructure/SqliteIntegrationTestCollection.cs` (`[CollectionDefinition("Sqlite")]` with `ICollectionFixture<SqliteApiFactory>`).
- [X] T066 Start Docker Desktop and run `dotnet test tests/PhoneBook.Api.IntegrationTests`. With zero tests, confirm the container fixture starts and stops cleanly. Add one smoke test, `Infrastructure/HealthTests.cs`, asserting that `GET /health/ready` returns 200. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: The foundation is ready: the domain core, the CQRS pipeline, both database providers, the API host and the test harness. User stories can now begin, in parallel if staffed.

---

## Phase 3: User Story 1 - Add a contact to the phone book (Priority: P1) 🎯 MVP

**Goal**: `POST /api/v1/contacts` creates a validated contact that follows the duplicate rule (FR-001–FR-005, FR-018). `GET /api/v1/contacts/{id}` (ReadDbContext) supports the `Location` header.

**Independent Test**:
- POST a valid contact and receive `201` with `Location` and `ETag: "1"`.
- GET the Location and receive the same data.
- Invalid input returns `400` ValidationProblemDetails that name each field.
- The same phone number under the same tag returns `409`.

### Tests for User Story 1 ⚠️ (write first; they must fail)

- [X] T067 [P] [US1] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Commands/CreateContactCommandTests.cs` (through `Sender`):
  - valid command → success, and the row exists in the DB with the normalized phone
  - same phone and same tag (different case, `" HAMKAR "` vs `"hamkar"`) → `Contact.Duplicate`
  - same phone and a different tag → success
  - blank last name plus phone `"abc"` → one `ValidationError` with fields `lastName` and `phoneNumber`
  - nothing is persisted after a validation failure
- [X] T068 [P] [US1] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactByIdQueryTests.cs`: a seeded contact is returned with the correct `Version`, and an unknown id gives `Contact.NotFound`.
- [X] T069 [P] [US1] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/CreateContactEndpointTests.cs` (HTTP):
  - `201`, with a `Location` header that resolves via GET to `200`, `ETag: "1"`, and a body matching `ContactResponse`
  - `400` `application/problem+json` with `errors.lastName` and `errors.phoneNumber` and `errorCode = "General.Validation"`
  - `409` with `errorCode = "Contact.Duplicate"`
  - `401` with `X-Test-Scopes: none`, whose body is `application/problem+json` with `errorCode = "Auth.Unauthorized"`
  - `403` with `X-Test-Scopes: phonebook.read`, whose body is `application/problem+json` with `errorCode = "Auth.Forbidden"` *(constitution Principle III)*
- [X] T070 [P] [US1] Integration test `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/CreateContactConcurrencyTests.cs` *(C3)*: fire 20 identical `POST`s (same phone and tag) in parallel with `Task.WhenAll`. Assert that exactly one returns `201` and the other 19 return `409` with `errorCode = "Contact.Duplicate"`. This covers both the domain-service check and the unique-index fallback (the PostgreSQL `23505` translation). Assert there are no `500`s and exactly one row in the DB.
- [X] T071 [P] [US1] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/GetContactByIdEndpointTests.cs`: `200` with an `ETag`, `404` for an unknown GUID, and `400` `Contact.Id.Invalid` for `/api/v1/contacts/not-a-guid` (the spec's malformed-identifier edge case). The same malformed URL called with `X-Test-Scopes: none` returns `401` with `errorCode = "Auth.Unauthorized"` *(K3)*.

### Implementation for User Story 1

- [X] T072 [P] [US1] Create `src/PhoneBook.Application/Contacts/Create/CreateContactCommand.cs` (`sealed record CreateContactCommand(string FirstName, string LastName, string PhoneNumber, string Tag) : ICommand<ContactResponse>`) and `CreateContactCommandValidator.cs`:
  - FluentValidation with `NotEmpty` on all four fields
  - `MaximumLength(PersonName.MaxLength)` (= 100) on the names
  - `MaximumLength(Tag.MaxLength)` (= 50) on the tag
  - `MaximumLength(64)` on the raw phone (a shape guard only; the domain rule is authoritative) *(I5)*
  - reference the domain constants directly, with no duplicated magic numbers *(D1)*
  - error codes that match the domain codes: `PersonName.FirstName.Required`, etc.
- [X] T073 [US1] Create `src/PhoneBook.Application/Contacts/Create/CreateContactCommandHandler.cs`:
  1. Builds `PersonName`, `PhoneNumber` and `Tag`, and uses `Result.Combine` so that all field errors are reported together.
  2. Calls `ContactDuplicateChecker.EnsureNotDuplicateAsync(phone, tag, excluding: null)`.
  3. Calls `Contact.Create`.
  4. Calls `repository.Add`.
  5. Returns `ContactResponse.FromDomain(contact)`.
  - It does **not** call `SaveChanges`, because `UnitOfWorkBehavior` does that.
- [X] T074 [P] [US1] Create `src/PhoneBook.Application/Contacts/GetById/GetContactByIdQuery.cs` (`IQuery<ContactResponse>` with `Guid Id`) and `GetContactByIdQueryHandler.cs`: uses `IReadDbContext.Contacts.Where(c => c.Id == id).Select(...)` with `FirstOrDefaultAsync` (EF async via `Microsoft.EntityFrameworkCore`), and returns `ContactErrors.NotFound` when the result is null.
- [X] T075 [P] [US1] Create `src/PhoneBook.Application/Contacts/EventHandlers/ContactCreatedAuditHandler.cs` (`INotificationHandler<DomainEventNotification<ContactCreatedDomainEvent>>`): logs `"Contact {ContactId} created with tag {Tag}"` at Information level.
- [X] T076 [US1] Create `src/PhoneBook.Api/Endpoints/Contacts/CreateContact.cs` (`IEndpoint`):
  - `MapPost("contacts", ...)` with a request record `ContactRequest(string FirstName, string LastName, string PhoneNumber, string Tag)`
  - maps the request to the command
  - on success, returns `Results.CreatedAtRoute("GetContactById", new { id, version = "1" }, body)` and sets the `ETag` header
  - on failure, returns `result.ToProblem()`
  - `.RequireAuthorization(Policies.ContactsWrite)`, `.WithName("CreateContact")`, `.WithTags("Contacts")`, `.Produces<ContactResponse>(201)`, `.ProducesValidationProblem()`, `.ProducesProblem(409)`
  - Put `ContactRequest` in `src/PhoneBook.Api/Endpoints/Contacts/ContactRequest.cs`.
- [X] T077 [US1] Create `src/PhoneBook.Api/Endpoints/Contacts/GetContactById.cs`: `MapGet("contacts/{id:guid}", ...)`, named `"GetContactById"`, with `.RequireAuthorization(Policies.ContactsRead)` and the `ETag` header. Also create `src/PhoneBook.Api/Endpoints/Contacts/MalformedContactId.cs`, which registers `app.MapMethods("contacts/{id}", ["GET", "PUT", "DELETE"], ...)`. Because it has no route constraint, it ranks below the `{id:guid}` routes, so it only matches non-GUID ids. It returns a `400` ValidationProblem with field `id` and code `Contact.Id.Invalid`, and calls `.RequireAuthorization()` (any authenticated client; no scope needed), so anonymous callers get a `401` ProblemDetails (constitution Principle VI) *(K3)*. This covers the spec's malformed-identifier edge case for US1, US3 and US4 *(U4)*.
- [X] T078 [US1] Run `dotnet test --filter "FullyQualifiedName~CreateContact|GetContactById"` (the domain tests already ran in Phase 2) *(W2)* for fast feedback. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: US1 works on its own (the MVP): contacts can be created and read back through Swagger.

---

## Phase 4: User Story 2 - Find all contacts with a given tag (Priority: P1)

**Goal**: `GET /api/v1/contacts?tag=…` returns every contact with that tag, served by **Dapper** (FR-008, FR-009). Matching is exact, ignores case and ignores surrounding spaces. No matches gives an empty array.

**Independent Test**: Seed contacts directly through the DB helper (no dependency on the US1 endpoint), query a tag, and check that exactly the matching items are returned, ordered by last name then first name.

### Tests for User Story 2 ⚠️

- [X] T079 [P] [US2] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactsByTagQueryTests.cs` (seed with `SeedContactAsync`):
  - 3 × `"همکار"` and 2 × `"خانواده"`: querying `"همکار"` returns exactly 3
  - `"Work"` is matched by `" work "`
  - `"همکار"` does not match `"همکاران"`
  - an unknown tag returns an empty list (success)
  - results are ordered by last name then first name using **ordinal** comparison. Include Persian names, for example `"احمدی"` before `"رضایی"`, and a Latin/Persian mix, and compare against `StringComparer.Ordinal` *(P1)*
- [X] T080 [P] [US2] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/GetContactsByTagEndpointTests.cs`:
  - `200` with a JSON array
  - `200 []` for an unknown tag
  - `400` for `?tag=` and for a missing `tag`
  - `400` for a 51-character tag
  - `401` for `none`
  - `200` with the read-only scope `phonebook.read`

### Implementation for User Story 2

- [X] T081 [P] [US2] Create `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQuery.cs` (`IQuery<IReadOnlyList<ContactResponse>>` with `string Tag`) and `GetContactsByTagQueryValidator.cs` (`NotEmpty` after trim, code `Tag.Required`; `MaximumLength(Tag.MaxLength)` (= 50), code `Tag.TooLong`) *(D2)*.
- [X] T082 [US2] Create `src/PhoneBook.Application/Contacts/GetByTag/GetContactsByTagQueryHandler.cs`:
  - uses `ISqlConnectionFactory` and **Dapper** `QueryAsync<ContactResponse>` with the SQL (verbatim from data-model §3): `SELECT id AS Id, first_name AS FirstName, last_name AS LastName, phone_number AS PhoneNumber, tag AS Tag, version AS Version FROM contacts WHERE normalized_tag = @NormalizedTag`. **There is no `ORDER BY`** (research R-07, P1). After the query, sort in memory with `.OrderBy(c => c.LastName, StringComparer.Ordinal).ThenBy(c => c.FirstName, StringComparer.Ordinal)`, so the order is identical on SQLite and PostgreSQL (spec FR-008)
  - `@NormalizedTag = Tag.Normalize(query.Tag)`
  - Add a Dapper `SqlMapper.TypeHandler<Guid>` in `src/PhoneBook.Infrastructure/Persistence/Dapper/GuidTypeHandler.cs`, registered in `AddInfrastructure`, because SQLite returns GUIDs as TEXT.
  - Returns `Result<IReadOnlyList<ContactResponse>>`.
- [X] T083 [US2] Create `src/PhoneBook.Api/Endpoints/Contacts/GetContactsByTag.cs`:
  - `MapGet("contacts", ([FromQuery] string? tag, ISender sender, CancellationToken ct) => ...)`
  - `.RequireAuthorization(Policies.ContactsRead)`
  - `.Produces<IReadOnlyList<ContactResponse>>(200)` and `.ProducesValidationProblem()`
- [X] T084 [US2] Run the US2 tests with `dotnet test --filter "FullyQualifiedName~GetContactsByTag"` for fast feedback. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: US1 and US2 together give a usable phone book (add and find).

---

## Phase 5: User Story 3 - Edit an existing contact (Priority: P2)

**Goal**: `PUT /api/v1/contacts/{id}` replaces all four fields and keeps the id (FR-006, FR-007). It enforces the duplicate rule while excluding the contact itself (FR-018), supports `If-Match` (412), and maps concurrency conflicts to `409` (FR-017).

**Independent Test**: Seed a contact, send a PUT with new values, and check `200`, `version == 2`, `ETag: "2"` and a DB row with the new values. A stale `If-Match` gives `412`.

### Tests for User Story 3 ⚠️

- [X] T085 [P] [US3] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/ContactUpdateTests.cs`:
  - changing a field increments `Version`, sets `UpdatedAtUtc` and raises `ContactUpdatedDomainEvent`
  - changing the tag also raises `ContactTagChangedDomainEvent(old, new)`
  - a tag change that differs only in case/whitespace (`"Work"` → `"work"`) is *not* a tag change, but it still updates the display value
  - identical values → success with no events and no version change
  - `Id` is unchanged
- [X] T086 [P] [US3] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Commands/UpdateContactCommandTests.cs`:
  - success updates the row and the version
  - unknown id → `Contact.NotFound`
  - invalid values → `ValidationError`, and the row is unchanged
  - changing the phone and tag to match *another* contact → `Contact.Duplicate`
  - updating a contact with its own current phone and tag → success (it is excluded from the duplicate check)
  - `ExpectedVersion = 99` → `Contact.VersionMismatch`
  - after the tag changes from `"همکار"` to `"دوست"`, `GetContactsByTagQuery("همکار")` is empty and `("دوست")` contains the contact (US3-2; uses the US2 query, so run it after US2 or assert through `IReadDbContext`)
- [X] T087 [P] [US3] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/UpdateContactEndpointTests.cs`:
  - `200` with `ETag: "2"`
  - `404`
  - `400` for a blank last name
  - `400` for a non-GUID id
  - `412` with `If-Match: "1"` after the contact has already been updated
  - `409` duplicate
  - `403` with the read scope only
- [X] T088 [P] [US3] Integration test `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/ConcurrentUpdateTests.cs` (SC-005): fire 10 parallel PUTs with `If-Match: "1"` at the same contact. Assert that exactly one returns `200` and all the others return `412` or `409`, and that the final DB `version == 2`.

### Implementation for User Story 3

- [X] T089 [US3] Add `Result Update(PersonName name, PhoneNumber phone, Tag tag, IDateTimeProvider clock)` to `src/PhoneBook.Domain/Contacts/Contact.cs`:
  - if the name, phone and tag Value are all equal to the current values, return success and do nothing else
  - otherwise capture `oldTag`, assign the new values, `Version++`, `UpdatedAtUtc = clock.UtcNow`, raise `ContactUpdatedDomainEvent`, and raise `ContactTagChangedDomainEvent(Id, oldTag.Value, tag.Value)` when `!oldTag.Equals(tag)`
- [X] T090 [P] [US3] Create `src/PhoneBook.Application/Contacts/Update/UpdateContactCommand.cs` (`sealed record UpdateContactCommand(Guid Id, string FirstName, string LastName, string PhoneNumber, string Tag, int? ExpectedVersion) : ICommand<ContactResponse>`) and `UpdateContactCommandValidator.cs` (the same rules as the create validator, plus `Id` not empty and `ExpectedVersion > 0` when present).
- [X] T091 [US3] Create `src/PhoneBook.Application/Contacts/Update/UpdateContactCommandHandler.cs`:
  1. Builds the value objects (combining their errors).
  2. Loads the contact with `repository.GetByIdAsync`, returning `NotFound` if it is missing.
  3. If `ExpectedVersion` is set and differs from `contact.Version`, returns `ContactErrors.VersionMismatch`.
  4. Calls `duplicateChecker.EnsureNotDuplicateAsync(phone, tag, excluding: contact.Id)`.
  5. Calls `contact.Update(...)`.
  6. Returns `ContactResponse.FromDomain(contact)`.
- [X] T092 [P] [US3] Create `src/PhoneBook.Application/Contacts/EventHandlers/ContactUpdatedAuditHandler.cs` and `ContactTagChangedAuditHandler.cs` (structured Information logs).
- [X] T093 [US3] Create `src/PhoneBook.Api/Endpoints/Contacts/UpdateContact.cs`:
  - `MapPut("contacts/{id:guid}", ...)` with the `ContactRequest` body
  - `ETagExtensions.TryParseIfMatch(request)` becomes `ExpectedVersion`
  - on success returns `200` with the body and `ETag`
  - `.RequireAuthorization(Policies.ContactsWrite)`
  - `.Produces<ContactResponse>(200)`, `.ProducesValidationProblem()`, `.ProducesProblem(404)`, `.ProducesProblem(409)`, `.ProducesProblem(412)`
  - Malformed ids are already handled by the `MapMethods` fallback from T077 *(U4)*.
- [X] T094 [US3] Run the US3 tests with `dotnet test --filter "FullyQualifiedName~Update"` for fast feedback. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: US1, US2 and US3 all work independently.

---

## Phase 6: User Story 4 - Delete a contact (Priority: P2)

**Goal**: `DELETE /api/v1/contacts/{id}` removes the contact and returns `204`. An unknown or already-deleted id returns `404` (FR-010, FR-011). An optional `If-Match` header gives `412` on a mismatch.

**Independent Test**: Seed a contact, DELETE it and get `204`. A second DELETE gives `404`. A tag query no longer returns the contact, checked through the DB or through the US2 query.

### Tests for User Story 4 ⚠️

- [X] T095 [P] [US4] Unit tests `tests/PhoneBook.Domain.UnitTests/Contacts/ContactDeleteTests.cs`: `MarkAsDeleted` returns success and raises exactly one `ContactDeletedDomainEvent` with the id and tag.
- [X] T096 [P] [US4] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Commands/DeleteContactCommandTests.cs`:
  - success removes the row
  - a second delete → `Contact.NotFound`
  - a random id → `Contact.NotFound`
  - `ExpectedVersion` mismatch → `Contact.VersionMismatch`, and the row still exists
- [X] T097 [P] [US4] Integration tests `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/DeleteContactEndpointTests.cs`:
  - `204` with no body
  - a repeat delete → `404`
  - `400` for a non-GUID id
  - `412` for a stale `If-Match`
  - `403` with the read scope
  - after the delete, `GET /contacts?tag=` no longer contains the contact

### Implementation for User Story 4

- [X] T098 [US4] Add `Result MarkAsDeleted()` to `src/PhoneBook.Domain/Contacts/Contact.cs`. It raises `ContactDeletedDomainEvent(Id, Tag.Value)` and returns success.
- [X] T099 [P] [US4] Create `src/PhoneBook.Application/Contacts/Delete/DeleteContactCommand.cs` (`sealed record DeleteContactCommand(Guid Id, int? ExpectedVersion) : ICommand`) and `DeleteContactCommandValidator.cs` (`Id` not empty; `ExpectedVersion > 0` when present).
- [X] T100 [US4] Create `src/PhoneBook.Application/Contacts/Delete/DeleteContactCommandHandler.cs`:
  1. Loads the contact, returning `NotFound` if it is missing.
  2. Checks the version and returns `VersionMismatch` if it differs.
  3. Calls `contact.MarkAsDeleted()`.
  4. Calls `repository.Remove(contact)`.
  5. Returns `Result.Success()`.
  - Ensure `WriteDbContext` still collects events from entities in the `Deleted` state (T046 collects from all tracked `IHasDomainEvents` entries; verify this).
- [X] T101 [P] [US4] Create `src/PhoneBook.Application/Contacts/EventHandlers/ContactDeletedAuditHandler.cs` (structured Information log).
- [X] T102 [US4] Create `src/PhoneBook.Api/Endpoints/Contacts/DeleteContact.cs`:
  - `MapDelete("contacts/{id:guid}", ...)`
  - reads `If-Match`
  - `204` on success
  - `.RequireAuthorization(Policies.ContactsWrite)`
  - `.Produces(204)`, `.ProducesProblem(404)`, `.ProducesProblem(409)`, `.ProducesProblem(412)`
  - Malformed ids are already handled by the `MapMethods` fallback from T077 *(U4)*.
- [X] T103 [US4] Run the US4 tests with `dotnet test --filter "FullyQualifiedName~Delete"` for fast feedback. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: All four user stories work end to end against the test authentication scheme.

---

## Phase 7: Identity server: separate OpenIddict app (FR-019, research R-12)

**Purpose**: A real authorization server issuing client-credentials tokens that the API validates. This phase can run **in parallel with Phases 3–6** once Phase 2 is done, because API tests use `TestAuthHandler`.

### Tests for the Identity server ⚠️

- [X] T104 [P] Create `tests/PhoneBook.Identity.IntegrationTests/Infrastructure/IdentityFactory.cs` (`WebApplicationFactory<IdentityAssemblyMarker>` *(I1)* that uses a unique in-memory SQLite database name per factory, e.g. `Data Source=identity-{Guid};Mode=Memory;Cache=Shared`).
- [X] T105 [P] Integration tests `tests/PhoneBook.Identity.IntegrationTests/DiscoveryTests.cs`: `/.well-known/openid-configuration` returns 200, `token_endpoint` ends with `/connect/token`, `grant_types_supported` contains `client_credentials`, and `jwks_uri` returns at least one key. **CORS** *(U1)*: an `OPTIONS /connect/token` preflight with `Origin: https://localhost:7001` and `Access-Control-Request-Method: POST` returns `Access-Control-Allow-Origin: https://localhost:7001`, while an unlisted origin gets no such header.
- [X] T106 [P] Integration tests `tests/PhoneBook.Identity.IntegrationTests/TokenEndpointTests.cs`:
  - `phonebook-swagger` with both scopes → `200` with `access_token`, `token_type == "Bearer"` and `expires_in > 0`
  - the decoded JWT (`System.IdentityModel.Tokens.Jwt` or `JsonWebTokenHandler.ReadJsonWebToken`) has `aud == "phonebook-api"` and a `scope` containing both scopes
  - a wrong secret → `invalid_client`
  - `phonebook-readonly` requesting `phonebook.write` → `invalid_scope`
  - `grant_type=password` → `unsupported_grant_type`
- [X] T107 Integration test `tests/PhoneBook.Identity.IntegrationTests/EndToEndTokenToApiTests.cs`:
  - start `IdentityFactory` and an API `WebApplicationFactory<ApiAssemblyMarker>` *(I1)* (Sqlite provider, **real OpenIddict validation**, no `TestAuthHandler`)
  - set the Identity `Identity:Issuer` and the API `Auth:Authority` to the identity factory's `Server.BaseAddress` (`http://localhost/`), and configure `OpenIddictValidationSystemNetHttpOptions` so its `HttpClient` primary handler is `identityFactory.Server.CreateHandler()`
  - get a token for `phonebook-swagger`, then `POST` a contact and expect `201`
  - get a token for `phonebook-readonly`, then `POST` and expect `403`, and `GET ?tag=` and expect `200`
  - no token → `401`
  - **Error contract with real OpenIddict** *(U6, constitution Principle III)*: assert that the real `401` (no token, and a malformed `Bearer abc` token) and `403` (read-only token on `POST`) responses have `Content-Type: application/problem+json`, the right `errorCode` (`Auth.Unauthorized` / `Auth.Forbidden`) and a `traceId`. The `WWW-Authenticate: Bearer` header must still be present on 401.
  - **If the real `403` assertion fails** *(U8)*, OpenIddict wrote its own body on forbid, so status-code pages were skipped. Extend the T056 handler to also handle the validation forbid path (the forbidden-response context of the installed OpenIddict version, the same technique as the 401 challenge) with `errorCode = "Auth.Forbidden"`, then re-run this test.

### Implementation for the Identity server

- [X] T108 [P] Create `src/PhoneBook.Identity/Data/IdentityDbContext.cs` (`DbContext` with `options.UseOpenIddict()` in `OnConfiguring`, or with `modelBuilder.UseOpenIddict()`) and `src/PhoneBook.Identity/Data/InMemorySqliteKeepAlive.cs`. The latter follows the same pattern as Infrastructure T043 but is a separate copy, because the Identity host has no project references.
- [X] T109 [P] Create `src/PhoneBook.Identity/Options/IdentityClientOptions.cs` (section `Identity:Clients`: a list of `{ ClientId, ClientSecret, DisplayName, Scopes[] }`), `src/PhoneBook.Identity/appsettings.json` (Serilog, `ConnectionStrings:Identity = "Data Source=identity;Mode=Memory;Cache=Shared"`, `Identity:Audience = "phonebook-api"`, `Identity:Issuer = "https://localhost:7002/"` *(U5)*, `Identity:AllowedCorsOrigins = ["https://localhost:7001", "http://localhost:5001"]` *(U1)*) and `src/PhoneBook.Identity/appsettings.Development.json`, with the dev clients:
  - `phonebook-swagger`: scopes `phonebook.read` and `phonebook.write`, secret `phonebook-swagger-dev-secret`
  - `phonebook-readonly`: scope `phonebook.read`, secret `phonebook-readonly-dev-secret`
  - Mark them **dev-only** with a comment in the README.
  - Also create `Properties/launchSettings.json` with profile `https` on `https://localhost:7002;http://localhost:5002`.
- [X] T110 Create `src/PhoneBook.Identity/Program.cs`:
  - Serilog
  - `AddDbContext<IdentityDbContext>(UseSqlite + UseOpenIddict)`
  - `AddOpenIddict()`:
    - `.AddCore(o => o.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())`
    - `.AddServer(o => { SetTokenEndpointUris("connect/token"); AllowClientCredentialsFlow(); RegisterScopes("phonebook.read","phonebook.write"); AddEphemeralEncryptionKey(); AddEphemeralSigningKey(); DisableAccessTokenEncryption(); UseAspNetCore().EnableTokenEndpointPassthrough(); })`
  - in `AddServer`, also call `SetIssuer(new Uri(config["Identity:Issuer"]))`. This fixes the token `iss` claim regardless of the request host, which the compose setup relies on *(U5)*.
  - **CORS** *(U1)*: `AddCors` with a named policy `"swagger"` using `WithOrigins(Identity:AllowedCorsOrigins)`, `AllowAnyHeader()` and `WithMethods("POST", "OPTIONS")`, and `app.UseCors("swagger")` before the endpoints. Without it, the Swagger UI "Authorize" call from the API origin is blocked by the browser.
  - health checks at `/health/live` and `/health/ready`
  - end the file with `public partial class Program;` and create `src/PhoneBook.Identity/IdentityAssemblyMarker.cs` (`public sealed class IdentityAssemblyMarker;`, namespace `PhoneBook.Identity`) *(I1)*
- [X] T111 Create `src/PhoneBook.Identity/Endpoints/TokenEndpoint.cs`, mapping `MapPost("connect/token", ...)`:
  - `HttpContext.GetOpenIddictServerRequest()`
  - if it is a client-credentials request: find the application with `IOpenIddictApplicationManager.FindByClientIdAsync`, then create a `ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)` with `Claims.Subject = clientId`, `Claims.Name = displayName`, `SetScopes(request.GetScopes())`, `SetResources("phonebook-api")` and destinations `AccessToken`, then `Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)`
  - otherwise return `Results.Forbid` with the OpenIddict error `unsupported_grant_type`
  - **No throws**
- [X] T112 Create `src/PhoneBook.Identity/Seeding/IdentitySeeder.cs` (`IHostedService`):
  - calls `EnsureCreatedAsync`
  - creates the scopes `phonebook.read` and `phonebook.write` with resource `phonebook-api` through `IOpenIddictScopeManager`
  - creates each configured client through `IOpenIddictApplicationManager`: `ClientType = Confidential`, permissions `Endpoints.Token`, `GrantTypes.ClientCredentials`, and `Prefixes.Scope + scope` for each allowed scope
  - is idempotent (skips existing entries)
- [X] T113 Run `dotnet test tests/PhoneBook.Identity.IntegrationTests` and confirm all tests pass, including the end-to-end test. Then run the **full suite**, `dotnet test PhoneBook.slnx` (both providers; Docker running), which must be entirely green, **and** `dotnet build PhoneBook.slnx -c Release` must report **0 warnings**, before the checkpoint is passed (constitution Principle V, workflow gate 4) *(K2, W1)*.

**Checkpoint**: The real OAuth2 flow works. Swagger "Authorize" obtains tokens from the Identity host.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T114 [P] Create `tests/PhoneBook.ArchitectureTests/LayerDependencyTests.cs` (NetArchTest.Rules):
  - Domain has no dependency on Application, Infrastructure, Api, `MediatR` or `Microsoft.EntityFrameworkCore`
  - Application has no dependency on Infrastructure or Api
  - Infrastructure has no dependency on Api
  - SharedKernel has no dependency on any other `PhoneBook.*`
  - Domain **and** SharedKernel have no dependency on `Microsoft.AspNetCore`, `MediatR` or `Microsoft.EntityFrameworkCore` (constitution Principle I) *(E1)*
  - Application has no dependency on `Microsoft.AspNetCore` or on provider drivers (`Npgsql`, `Microsoft.Data.Sqlite`). *Reconciled during implementation:* Dapper is allowed in Application, because the read-side query handler (T082) runs its SQL there through `ISqlConnectionFactory`; EF Core is allowed only for `IReadDbContext` query operators
  - classes implementing `IQueryHandler<,>` do not depend on `IContactRepository` or `WriteDbContext` (CQRS separation)
  - Also create `NamingConventionTests.cs`: command handlers end with `CommandHandler`, validators end with `Validator`, and domain events end with `DomainEvent` and are sealed.
- [X] T115 **SQLite provider smoke suite** (mandatory, constitution Principle V) *(C1)*: create `tests/PhoneBook.Api.IntegrationTests/Providers/SqliteProviderSmokeTests.cs` using `[Collection("Sqlite")]` and `SqliteApiFactory`, resetting before each test. Cover the code that runs only on SQLite:
  - `POST` → `201`, then `GET /contacts/{id}` → `200` (ReadDbContext on SQLite TEXT GUIDs)
  - `GET /contacts?tag=` returns the contact (Dapper plus `GuidTypeHandler`), and three Persian-named contacts under one tag come back in the same ordinal order as on PostgreSQL *(P1)*
  - the same phone and tag twice → `409` (domain service)
  - 20 parallel identical `POST`s → exactly one `201` and the rest `409`, never `500` (SQLite unique violation 2067 → `Contact.Duplicate`)
  - `PUT` with `If-Match: "1"` → `200`, and a stale one → `412`
  - 10 parallel `PUT`s with `If-Match: "1"` → exactly one `200` and no `500` (shared-cache locking and the concurrency token)
  - `DELETE` → `204`, and again → `404`
- [X] T116 [P] Mixed-load consistency test `tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/MixedConcurrencyTests.cs` (SC-005) *(C2)*:
  - seed 20 contacts, then fire **100** concurrent requests with `Task.WhenAll`: 40 creates with unique phones, 30 updates to distinct seeded contacts, 10 deletes of other seeded contacts, and 20 tag searches
  - assert: no response is a `5xx`
  - final row count = 20 + 40 − 10
  - every updated contact has `version == 2`
  - there are no duplicate `(phone_number, normalized_tag)` pairs
  - Run the same scenario against the SQLite factory as a second test in the same class.
- [X] T117 [P] Performance test `tests/PhoneBook.Api.IntegrationTests/Contacts/Queries/GetContactsByTagPerformanceTests.cs` (SC-004): bulk-insert 10,000 contacts through Dapper, 500 of them with the tag `"perf"`. Assert that `GetContactsByTagQuery("perf")` returns 500 in under 1 second, measured with `Stopwatch` after one warm-up call. Mark it with the trait `Category=Performance`. Add a second test in the same class that runs the same scenario against `SqliteApiFactory` (the default runtime provider), with the same `< 1 s` threshold *(C4)*.
- [X] T118 [P] Create `src/PhoneBook.Api/Dockerfile` and `src/PhoneBook.Identity/Dockerfile`:
  - multi-stage builds: the `mcr.microsoft.com/dotnet/sdk:10.0` build stage copies `Directory.*.props`, `global.json` and the csproj files first for layer caching; the runtime stage uses `mcr.microsoft.com/dotnet/aspnet:10.0`
  - run as the non-root `app` user
  - `EXPOSE 8080`
- [ ] T119 Create `docker-compose.yml` at the repository root:
  - `identity` (port 7002→8080, `ASPNETCORE_ENVIRONMENT=Development`, `Identity__Issuer=http://identity:8080/`, `Identity__AllowedCorsOrigins__0=http://localhost:7001`)
  - `api` (port 7001→8080, `Auth__Authority=http://identity:8080/`, `Auth__PublicAuthority=http://localhost:7002`, `Auth__RequireHttpsMetadata=false`, `depends_on: identity`). The browser fetches tokens from `localhost:7002`, but their issuer is fixed to `http://identity:8080/`, which the API container can reach for discovery and JWKS *(U5)*.
  - under the profile `postgres`: a `postgres:17-alpine` service, plus an `api` override using `Database__Provider=Postgres` and `Database__ConnectionString=Host=postgres;Database=phonebook;Username=postgres;Password=postgres`. Put a `# DEV-ONLY credentials. Never use them outside local development.` comment above this service and above the dev client secrets (constitution Principle VI) *(S1)*
  - Make `Auth:RequireHttpsMetadata` configurable in `AuthenticationSetup` (T056) through `UseSystemNetHttp` and `OpenIddictValidationOptions` so plain HTTP works inside compose. In Identity, call `DisableTransportSecurityRequirement()` only when `ASPNETCORE_ENVIRONMENT=Development`.
- [X] T120 [P] Create `.github/workflows/ci.yml` (on push/PR: `ubuntu-latest`, `actions/setup-dotnet` with `global-json-file`, `dotnet restore`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --logger trx --collect:"XPlat Code Coverage"`; Testcontainers works because Docker is available on the runner).
- [X] T121 Reconcile `specs/001-phonebook-management/` with the implementation (constitution Principle VII). The design docs were already brought in line during re-planning *(T1)*, so this task checks for drift:
  - every error code in `ContactErrors`, `ResultExtensions` and `CustomizeProblemDetails` appears in data-model §1.4
  - every mapped endpoint, status code and header matches `contracts/phonebook-api.openapi.yaml`, compared against the generated `/swagger/v1/swagger.json`
  - the Identity behaviour matches `contracts/identity-token.md`
  - Fix any drift in the code or the docs, and list what was reconciled in the README process section.
- [X] T122 Write `README.md` at the repository root as **the cover letter** (plan.md "Deliverable: README.md"). It must contain these sections, in this order:
  1. A short cover note to the Hasin Group (گروه حصین) reviewer
  2. The Persian brief, quoted, and a table mapping each brief bullet to an endpoint and spec requirement
  3. Architecture: a Mermaid layer diagram (SharedKernel ← Domain ← Application ← Infrastructure ← Api, plus the Identity host) and a Mermaid sequence diagram of the command path (endpoint → pipeline behaviors → handler → domain service → aggregate → UoW → events after commit)
  4. The DDD model: the aggregate, value objects, domain events, the domain service and the invariants table
  5. CQRS: the write path (repository + UoW) vs the read paths (ReadDbContext + Dapper), and why both are used
  6. Error handling: the Result pattern, the `ErrorType` → HTTP mapping table, and the two exception safety nets
  7. Persistence: the in-memory SQLite vs PostgreSQL provider switch, and why EF InMemory was rejected (research R-02)
  8. Security: the OpenIddict client-credentials flow, scopes and policies, the dev clients and their secrets, and production notes (certificates, auth code + PKCE)
  9. Technologies: a table with each package and why it was chosen, including the MediatR 12.5.0 and Shouldly licensing notes
  10. Testing strategy: the pyramid, what each project covers, Testcontainers + Respawn + WebApplicationFactory, the architecture tests, and how to run them
  11. How to run: local, Swagger walkthrough, compose (link to `specs/001-phonebook-management/quickstart.md`)
  12. **Development process**:
      - **Spec-Driven Development with GitHub Spec Kit**: how `/speckit-specify` turned the Persian PDF into `spec.md`, then `/speckit-plan` → research/data-model/contracts, `/speckit-tasks` → this task list, and `/speckit-implement`
      - **AI-assisted engineering with Claude Code**: which parts the AI drafted, how every output was reviewed, how the tests were the acceptance gate, and the human decisions made (for example the provider switch and the duplicate rule)
      - **Project constitution** (`.specify/memory/constitution.md` v1.0.1, including how the 1.0.1 amendment came out of an analysis finding): the seven principles, and how `/speckit-analyze` findings (for example the missing SQLite test coverage and the Swagger CORS gap) were caught *before* coding
      - links to the `specs/` artifacts as evidence
  13. Trade-offs and future work: transactional outbox, projected read store, persistent DB with migrations, pagination, rate limiting, OpenTelemetry, auth code + PKCE for end users
- [X] T123 Run `dotnet build PhoneBook.slnx -c Release` (0 warnings) and `dotnet test PhoneBook.slnx -c Release` (all green, with Docker running). Record the test counts in the README testing section.
- [X] T124 Execute every scenario in `specs/001-phonebook-management/quickstart.md` §3 (the 16-row table) manually through Swagger with both hosts running, and fix any deviation. Then tick the checklist in the README. *Done 2026-09-25: all 17 scenarios (the table grew to 17 rows during re-planning) were automated against both live HTTPS hosts with real tokens, 17/17 passing, plus a live CORS preflight check. Only the in-browser Swagger "Authorize" click, which means entering the dev client secret, is left to the reviewer.*

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1, T001–T009)** has no dependencies.
- **Foundational (Phase 2, T010–T066)** depends on Setup and **blocks all stories**. Inside Phase 2 the order is:
  1. SharedKernel (T010–T016)
  2. Domain unit tests written first (T017–T021, then T022, which must be red), then Domain code (T023–T031), then T032 (green) *(K1)*
  3. Application (T033–T041)
  4. Infrastructure (T042–T050)
  5. Api (T051–T060)
  6. Test harness (T061–T066)
- **US1 (Phase 3)** depends on Phase 2.
- **US2 (Phase 4)** depends on Phase 2. It is independent of US1, because tests seed through `SeedContactAsync`.
- **US3 (Phase 5)** depends on Phase 2. One scenario in T086 uses the US2 query, so either run it after US2 or assert through `IReadDbContext`.
- **US4 (Phase 6)** depends on Phase 2. The last assertion in T097 uses the US2 endpoint.
- **Identity (Phase 7)** depends on Phase 2 (T056, the API auth setup). It can run in parallel with Phases 3–6. T107 (end-to-end) needs the US1 and US2 endpoints.
- **Polish (Phase 8)** depends on all of the above. T115 (the SQLite smoke suite) and T116 (the 100-request mixed-load test) need every endpoint from US1–US4. Both are **mandatory** under the constitution's delivery gate and are not optional polish. T122 (README) should be written last so that it describes what was actually built.

### User Story Dependencies

```text
Phase1 ─► Phase2 ─┬─► US1 (P1, MVP) ─┐
                  ├─► US2 (P1) ──────┤
                  ├─► US3 (P2) ──────┼─► Phase 8 Polish
                  ├─► US4 (P2) ──────┤
                  └─► Identity ──────┘   (T107 E2E needs US1 + US2)
```

### Within Each User Story

- Tests are written first and must fail (red), including the Phase 2 domain tests *(K1)*.
- Then the domain changes, then the Application command/query, validator and handler, then the event handlers, then the API endpoint.
- The story checkpoint requires the **full** solution test suite, on both providers, to be green before moving on *(K2)*.

### Parallel Opportunities

- **Phase 1**: T004 runs in parallel with T001–T003.
- **Phase 2**:
  - SharedKernel: T010, T011, T013, T014, T015 and T016 together. T012 comes after T010 and T011.
  - Domain tests: T017–T021 together, then T022. Domain code: T023–T028 together, then T029, T030 and T031.
  - Application: T033–T040 together, then T041.
  - Infrastructure: T042, T043, T044, T048 and T049 alongside T045–T047.
  - Api: T051–T055 and T057 together.
  - Test harness: T061 and T062 together.
- **Within a story**: all test tasks marked [P] run together, and the command/query/validator files marked [P] run together.
- **Across stories**: once Phase 2 is done, US1, US2, US3, US4 and Identity can go to different developers or agents.

---

## Parallel Example: Phase 2 domain tests (write first)

```bash
Task: "T017 PersonNameTests"
Task: "T018 PhoneNumberTests"
Task: "T019 TagTests"
Task: "T020 ContactCreateTests"
Task: "T021 ContactDuplicateCheckerTests"
# then T022 (confirm red), then the domain code T023–T031, then T032 (green)
```

## Parallel Example: User Story 1

```bash
# All US1 tests together (different files):
Task: "T067 CreateContactCommandTests in tests/PhoneBook.Api.IntegrationTests/Contacts/Commands/CreateContactCommandTests.cs"
Task: "T069 CreateContactEndpointTests in tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/CreateContactEndpointTests.cs"
Task: "T070 CreateContactConcurrencyTests in tests/PhoneBook.Api.IntegrationTests/Contacts/Endpoints/CreateContactConcurrencyTests.cs"

# Then the Application pieces together:
Task: "T072 CreateContactCommand + Validator in src/PhoneBook.Application/Contacts/Create/"
Task: "T074 GetContactByIdQuery + Handler in src/PhoneBook.Application/Contacts/GetById/"
Task: "T075 ContactCreatedAuditHandler in src/PhoneBook.Application/Contacts/EventHandlers/"
```

## Parallel Example: User Story 3

```bash
Task: "T085 ContactUpdateTests (unit)"
Task: "T086 UpdateContactCommandTests (integration)"
Task: "T087 UpdateContactEndpointTests (HTTP)"
Task: "T088 ConcurrentUpdateTests (SC-005)"
# then
Task: "T090 UpdateContactCommand + Validator"
Task: "T092 Updated/TagChanged audit handlers"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup.
2. Phase 2: Foundational. This is the heaviest phase, because it holds all the architecture showcase work.
3. Phase 3: US1 (add a contact and read it back).
4. **Stop and validate**: the US1 tests are green, and a Swagger POST/GET works using the in-memory SQLite provider.

### Incremental Delivery

1. Setup + Foundational gives the foundation.
2. US1 gives create (the MVP).
3. US2 gives find-by-tag. The phone book is now useful, and both P1 stories are complete.
4. US3 gives edit, with ETag/If-Match and concurrency.
5. US4 gives delete.
6. The Identity phase adds real OAuth2. It can be done earlier, in parallel.
7. Polish covers architecture tests, performance, Docker, CI, the README cover letter and the final quickstart validation.

### Parallel Team / Multi-Agent Strategy

After Phase 2:
- Agent A takes US1 and then US3, since both use the create/update validators.
- Agent B takes US2 and then US4.
- Agent C takes Identity (Phase 7).
- Once all are done, one agent does Polish and the README.

---

## Notes

- Tasks marked [P] touch different files and have no dependencies on incomplete tasks.
- The `[USn]` labels trace each task back to the spec's user stories.
- Constraints are quoted verbatim from data-model.md in T024–T026, T045 and T082. Do not relax them.
- **Do not throw** in domain or application code. The only allowed `catch` blocks are in `UnhandledExceptionBehavior`, `GlobalExceptionHandler`, `WriteDbContext.SaveChangesAsync` (the persistence-to-`Result` translation) and `DomainEventDispatcher` (constitution Principle II).
- Delivery gate (constitution): before declaring completion, **both** providers' test suites must be green, the build must have 0 warnings, and the quickstart must be validated.
- Commit after each task or logical group, using the Co-Authored-By trailer if AI-assisted.
- Stop at any checkpoint to validate a story on its own.

---

## Phase 9: Convergence

- [ ] T125 Fix the CI test step in `.github/workflows/ci.yml`: Microsoft.Testing.Platform exits with code 5 on `--report-trx --coverage` because the extensions are not referenced. Add `Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage` to `Directory.Packages.props` and the four test projects, or drop the flags, then confirm a green GitHub Actions run, per T120 / Constitution V (contradicts)
- [ ] T126 Build and smoke-test the Docker Compose stack in `docker-compose.yml`. Default profile: get a token from `http://localhost:7002/connect/token`, then create and search through `http://localhost:7001` using the fixed issuer `http://identity:8080/`. Postgres profile: do the same through `http://localhost:7003`. Fix any issues with the HTTP issuer or discovery, per T119 / plan: research R-12, U5 (partial)
- [X] T127 Either apply `AuthOptions.RequireHttpsMetadata` to the OpenIddict validation configuration in `src/PhoneBook.Api/Infrastructure/Auth/AuthenticationSetup.cs`, or remove the option from `AuthOptions.cs` and `appsettings.json` if OpenIddict has no equivalent switch, per T055 / T119 (partial)
- [X] T128 Remove the unused `AspNetCore.HealthChecks.NpgSql` entry from `Directory.Packages.props`, or reference it for a PostgreSQL readiness check in `src/PhoneBook.Infrastructure/DependencyInjection.cs`, per plan: health checks (unrequested)
- [ ] T129 With both hosts running, do the in-browser Swagger UI walkthrough at `https://localhost:7001/swagger`: Authorize as `phonebook-swagger`, then call POST and GET. Tick the remaining item in the README quickstart checklist, per T124 / FR-015 / SC-001 (partial)
- [X] T130 Record in `specs/001-phonebook-management/research.md` the two implementation decisions that differ from task wording: validators delegate to the domain value-object factories (`src/PhoneBook.Application/Contacts/ContactFieldRules.cs`) instead of NotEmpty/MaximumLength rules (T072), and the end-to-end test supplies the Identity issuer configuration statically instead of swapping HttpClient handlers (T107), per Constitution VII (contradicts)
