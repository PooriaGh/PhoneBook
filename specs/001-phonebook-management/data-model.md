# Data Model: Phone Book Management

**Feature**: `001-phonebook-management` | **Date**: 2026-09-25 | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

## 1. Domain model (write side)

```text
┌─────────────────────────── Contact «Aggregate Root» ───────────────────────────┐
│ Id        : ContactId «VO»        (Guid v7, strongly typed)                     │
│ Name      : PersonName «VO»       { FirstName, LastName }                       │
│ Phone     : PhoneNumber «VO»      { Value (normalized) }                        │
│ Tag       : Tag «VO»              { Value (display), NormalizedValue }          │
│ Version   : int                   (optimistic concurrency token, starts at 1)   │
│ CreatedAtUtc / UpdatedAtUtc : DateTime                                          │
│ DomainEvents : IReadOnlyCollection<IDomainEvent>                                │
│                                                                                 │
│ + static Create(name, phone, tag, IDateTimeProvider) : Result<Contact>          │
│ + Update(name, phone, tag, IDateTimeProvider)        : Result                   │
│ + MarkAsDeleted()                                    : Result                   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 1.1 Value objects

| VO | Factory | Invariants (all return `Result.Failure`, none throw) | Error codes |
|---|---|---|---|
| `ContactId` | `ContactId.New()`, `ContactId.From(Guid)` | The value is not `Guid.Empty` | `Contact.Id.Empty` |
| `PersonName` | `PersonName.Create(first, last)` | Each part is trimmed, not blank and at most 100 characters. Any Unicode is allowed, including the ZWNJ U+200C. | `PersonName.FirstName.Required`, `PersonName.FirstName.TooLong`, `PersonName.LastName.Required`, `PersonName.LastName.TooLong` |
| `PhoneNumber` | `PhoneNumber.Create(raw)` | Persian and Arabic-Indic digits are converted to ASCII. Then: an optional leading `+`, then only digits, spaces and `-`. After spaces and hyphens are removed, 4–15 digits remain. The stored value is `+?digits`. | `PhoneNumber.Required`, `PhoneNumber.InvalidCharacters`, `PhoneNumber.InvalidLength` |
| `Tag` | `Tag.Create(raw)` | Trimmed, not blank, at most 50 characters. `NormalizedValue = Value.ToUpperInvariant()`. Two tags are equal when their `NormalizedValue` matches. | `Tag.Required`, `Tag.TooLong` |

Value objects compare equal when their components are equal. They are immutable `sealed record`s with private constructors. **Exception:** `Tag` is a `sealed class` that implements `IEquatable<Tag>`. Its equality must use **only** `NormalizedValue` while the display `Value` is kept, and a record's generated equality would compare both.

Every value-object validation failure is returned as `ValidationError.For(field, code, description)` (one `FieldError`), so errors can be combined per field (FR-012).

### 1.2 Aggregate behaviour and state transitions

```text
          Create() ──► [Active, Version=1]  ──Update()──► [Active, Version=n+1]
                              │                                   │
                              └────────── MarkAsDeleted() ────────┴──► [Removed from repository]
```

| Operation | Rules | Domain events raised |
|---|---|---|
| `Create` | The value objects are already valid, and the duplicate check has passed (the domain service runs before this) | `ContactCreatedDomainEvent(ContactId, PhoneNumber, Tag)` |
| `Update` | If nothing changed, the result is a success with no event and no version increase. Otherwise the fields are replaced, `Version` increases and `UpdatedAtUtc` is set. | `ContactUpdatedDomainEvent(ContactId)`, plus `ContactTagChangedDomainEvent(ContactId, OldTag, NewTag)` when the tag changes |
| `MarkAsDeleted` | Raises the event before the repository removes the entity | `ContactDeletedDomainEvent(ContactId, Tag)` |

### 1.3 Domain service

**`ContactDuplicateChecker`** (Domain), with the port `IContactUniquenessReader` (defined in Domain and implemented in Infrastructure):

```text
EnsureNotDuplicateAsync(PhoneNumber phone, Tag tag, ContactId? excluding, ct) : Task<Result>
  → Failure(Error.Conflict "Contact.Duplicate") when another contact has the same phone.Value AND tag.NormalizedValue
```

This is spec rule FR-018: the same number may appear under different tags, but not twice under one tag.

### 1.4 Domain errors (`ContactErrors`)

| Code | Type | HTTP |
|---|---|---|
| `Contact.NotFound` | NotFound | 404 |
| `Contact.Duplicate` | Conflict | 409 |
| `Contact.ConcurrencyConflict` | Conflict | 409 |
| `Contact.VersionMismatch` | PreconditionFailed | 412 |
| `*.Required` / `*.TooLong` / `PhoneNumber.*` | Validation | 400 |
| `Contact.Id.Invalid` | Validation (malformed route id) | 400 |
| `Request.IfMatch.Invalid` | Validation (malformed `If-Match`) | 400 |
| `PhoneNumber.TooLong` | Validation (raw phone input over 64 characters, a shape guard before domain parsing) | 400 |
| `General.Validation` | Validation (wrapper of `FieldError`s) | 400 |
| `Auth.Unauthorized` | (framework) missing or invalid token | 401 |
| `Auth.Forbidden` | (framework) missing scope | 403 |
| `General.NotFound` / `General.MethodNotAllowed` | (framework) unknown route / method | 404 / 405 |
| `General.Error` | (framework) any other error status without a specific code | 4xx/5xx |
| `General.Unexpected` | Unexpected | 500 |

*Reconciled during implementation (T121): `PhoneNumber.TooLong` and `General.Error` were added after comparing this table with the codes defined in `src/`.*

## 2. Persistence schema (shared by both providers, snake_case)

**Table `contacts`**

| Column | Type (SQLite / PostgreSQL) | Null | Notes |
|---|---|---|---|
| `id` | TEXT / uuid | no | PK (`ContactId` converted) |
| `first_name` | TEXT / varchar(100) | no | |
| `last_name` | TEXT / varchar(100) | no | |
| `phone_number` | TEXT / varchar(16) | no | normalized |
| `tag` | TEXT / varchar(50) | no | display value |
| `normalized_tag` | TEXT / varchar(50) | no | **index** `ix_contacts_normalized_tag` |
| `version` | INTEGER / integer | no | **concurrency token** |
| `created_at_utc` | TEXT / timestamptz | no | |
| `updated_at_utc` | TEXT / timestamptz | yes | |

- **Unique index** `ux_contacts_phone_tag (phone_number, normalized_tag)`. This is a safety net for FR-018. The domain service is the primary guard.
- The schema is created with `EnsureCreated()`, so there are no migrations (see research R-02).

The OpenIddict tables (`openiddict_applications`, `openiddict_scopes`, `openiddict_tokens`, `openiddict_authorizations`) live **in the Identity host's own in-memory database**, not in this one.

## 3. Read models (read side)

| Read model | Source | Used by | Technology |
|---|---|---|---|
| `ContactReadModel { Id, FirstName, LastName, PhoneNumber, Tag, NormalizedTag, Version, CreatedAtUtc, UpdatedAtUtc }` | `contacts` table mapped by `ReadDbContext` (no tracking) | `GetContactByIdQuery` | EF Core LINQ projection |
| `ContactResponse { Id, FirstName, LastName, PhoneNumber, Tag, Version }` (DTO) | Raw SQL `SELECT id AS Id, first_name AS FirstName, last_name AS LastName, phone_number AS PhoneNumber, tag AS Tag, version AS Version FROM contacts WHERE normalized_tag = @NormalizedTag`. There is **no `ORDER BY`**; the handler sorts with `StringComparer.Ordinal` by `LastName` then `FirstName` (research R-07, P1). | `GetContactsByTagQuery` | Dapper through `ISqlConnectionFactory` |

The query side never loads aggregates and never calls `SaveChanges`. `ReadDbContext` overrides `SaveChanges` so that it fails, which makes it read-only by construction.

## 4. Application messages (CQRS)

| Message | Kind | Returns | Validator |
|---|---|---|---|
| `CreateContactCommand(FirstName, LastName, PhoneNumber, Tag)` | `ICommand<ContactResponse>` | `Result<ContactResponse>` | `CreateContactCommandValidator` |
| `UpdateContactCommand(Id, FirstName, LastName, PhoneNumber, Tag, ExpectedVersion?)` | `ICommand<ContactResponse>` | `Result<ContactResponse>` | `UpdateContactCommandValidator` |
| `DeleteContactCommand(Id, ExpectedVersion?)` | `ICommand` | `Result` | `DeleteContactCommandValidator` |
| `GetContactByIdQuery(Id)` | `IQuery<ContactResponse>` | `Result<ContactResponse>` | none |
| `GetContactsByTagQuery(Tag)` | `IQuery<IReadOnlyList<ContactResponse>>` | `Result<IReadOnlyList<…>>` | `GetContactsByTagQueryValidator` (tag not blank, at most 50 characters) |

## 5. Mapping of spec entities → model

| Spec entity | Model element |
|---|---|
| Phone Book Entry (Contact) | `Contact` aggregate root |
| Phone Number | `PhoneNumber` value object |
| Tag | `Tag` value object |
| Phone Book (collection) | `IContactRepository` (write) + `IReadDbContext` / Dapper (read) |
