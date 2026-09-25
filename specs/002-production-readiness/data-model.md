# Data Model: Production Readiness

**Feature**: `002-production-readiness` | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

The `Contact` aggregate and its value objects are **unchanged** (see
[`../001-phonebook-management/data-model.md`](../001-phonebook-management/data-model.md)).

## 1. Paging (read side)

### `GetContactsByTagQuery`: extended

| Field | Type | Rule | Error code (field) |
|---|---|---|---|
| `Tag` | string | unchanged: not blank after trimming, at most 50 characters | `Tag.Required`, `Tag.TooLong` (`tag`) |
| `Page` | int | ≥ 1; default 1 | `Paging.Page.Invalid` (`page`) |
| `PageSize` | int | 1–200; default 50 | `Paging.PageSize.Invalid` (`pageSize`) |

All violations are reported together in one `ValidationError` (feature 001, FR-012).

**Parsing query values** (analyze U3):
- **Absent or empty** (`page=`): the default is used.
- **Not a whole number**, or outside the `int` range (`page=abc`, `page=99999999999`): the value is treated as
  invalid and reported with the field's code above. It is never a framework binding error.

### `PagedResponse<T>` (Application DTO)

| Field | Type | Meaning |
|---|---|---|
| `Items` | `IReadOnlyList<T>` | the contacts on this page (`ContactResponse`) |
| `Page` | int | the page returned |
| `PageSize` | int | the page size used |
| `TotalCount` | int | number of contacts matching the tag |
| `HasNext` | bool | `Page * PageSize < TotalCount` |

A page beyond the last one returns empty `Items` with the correct `TotalCount` and `HasNext = false`.

**Consistency**: every page is computed from the data at the moment it is requested. If contacts are added or
deleted between two page requests, a contact can appear twice or be skipped (spec edge case). Keyset paging,
which avoids this, is future work.

### Ordering and index

- **Order:** `last_name, first_name, id`, using byte-order collation (SQLite `BINARY`, PostgreSQL `LC_COLLATE=C`;
  see research R-02).
- **Index:** `ix_contacts_tag_order (normalized_tag, last_name, first_name, id)` replaces
  `ix_contacts_normalized_tag`.
- **Unchanged:** the unique index `ux_contacts_phone_tag`.

## 2. Rate limiting (configuration, not persisted)

| Policy | Applies to | Partition key | Default | Config keys |
|---|---|---|---|---|
| `api` | all `/api/v1/*` routes | `sub` claim, otherwise remote IP | 100 per 60 s | `RateLimiting:Api:PermitLimit`, `:WindowSeconds` |
| `token` | Identity `POST /connect/token`, `POST /account/login` | remote IP | 10 per 60 s | `RateLimiting:Token:PermitLimit`, `:WindowSeconds` |
| none | `/health/*`, `/swagger/*` (API); `/.well-known/*` (Identity discovery and JWKS) | none | unlimited | none |

These are fixed windows with no queueing. The limiter runs before the authentication or authorization outcome
(FR-007), so an over-limit request gets `429` even if it would otherwise get `401` or `403`.

On rejection the response is `429` with `Retry-After`: whole seconds, rounded up, at least 1, and no other
rate-limit headers. The body depends on the endpoint:

| Endpoint | 429 body |
|---|---|
| API routes | ProblemDetails with `errorCode` `RateLimit.Exceeded` |
| `POST /connect/token` | OAuth JSON `temporarily_unavailable` (research R-08) |
| `POST /account/login` | the sign-in HTML page with "Too many attempts. Try again in N seconds." |

There is no per-account lockout (the user's decision).

## 3. Telemetry names

| Kind | Name | Tags (no personal data) |
|---|---|---|
| ActivitySource | `PhoneBook.Application` | `phonebook.request` = request type name, `phonebook.result` = `success` or the error code |
| ActivitySource | `PhoneBook.Persistence` | `db.system` = `sqlite` or `postgresql`, `db.operation` = `save` or `query.contacts_by_tag` |
| Meter `PhoneBook` | `phonebook.contacts.created` / `.updated` / `.deleted` (counters) | none |
| Meter `PhoneBook` | `phonebook.ratelimit.rejections` (counter) | `policy` = `api` or `token` |
| Built-in | `http.server.request.duration` (histogram) | `http.route`, `http.response.status_code`, `http.request.method` |

**Removed from spans:** `url.query`, which carries the tag value, and `client.address`.

**Never recorded in telemetry or logs** (FR-016): contact names, phone numbers, tag values, end-user user names,
passwords, client secrets, tokens, client network addresses and request bodies.

**`traceId` in ProblemDetails:** the 32-hex W3C trace id (`Activity.TraceId`), falling back to
`HttpContext.TraceIdentifier`.

## 4. Error codes added

| Code | Type | HTTP | Where |
|---|---|---|---|
| `Paging.Page.Invalid` | Validation | 400 | tag search |
| `Paging.PageSize.Invalid` | Validation | 400 | tag search |
| `RateLimit.Exceeded` | (framework) | 429 | any rate-limited API route |
| `temporarily_unavailable` (OAuth `error`) | none | 429 | Identity token endpoint. The login form gets an HTML page instead. |
| `access_denied` (OAuth `error`, on redirect) | none | 302 | authorize, when no requested scope is held by the user or `scope` is absent |

## 5. End users (Identity host, in memory)

### `IdentityUser` (configuration → memory)

| Field | Source | Notes |
|---|---|---|
| `Id` | derived: a stable GUID from the user name, created with a fixed namespace | becomes the `sub` claim |
| `UserName` | `Identity:Users[i]:UserName` | compared case-insensitively |
| `PasswordHash` | `PasswordHasher<IdentityUser>.HashPassword(Password)` at start-up | the plain text exists only in configuration |
| `DisplayName` | configuration | becomes the `name` claim |
| `Scopes` | configuration (`phonebook.read`, `phonebook.write`) | the upper bound for the scopes granted |

**Seeded (DEV-ONLY)** users:

| User name | Scopes |
|---|---|
| `alice` | read and write |
| `bob` | read only |

### Public client `phonebook-swagger-ui`

| Property | Value |
|---|---|
| Client type | `Public`, no secret |
| Consent | `Implicit` |
| Permissions | the authorization and token endpoints, the authorization-code grant, response type `code`, the `phonebook.read` and `phonebook.write` scopes |
| Requirement | PKCE, S256 only |
| Redirect URIs | from `Identity:SwaggerUi:RedirectUris` |

### Lifetimes and session (FR-025, FR-026)

| Item | Value |
|---|---|
| Authorization code | 5 min, single use; a replay revokes the tokens issued from it |
| End-user access token | 1 h, the same as client credentials |
| Sign-in cookie | 15 min, not sliding; `HttpOnly`, `SameSite=Lax`, Secure over HTTPS |
| Sign-out | none (out of scope) |

### Sign-in flow state

1. **Anonymous** → `GET /connect/authorize` → 302 `/account/login?ReturnUrl=…`
2. **Credentials POSTed**:
   - valid → a cookie is set → 302 `ReturnUrl`
   - invalid → the same form with a generic error and no cookie
3. **Authenticated** → `GET /connect/authorize` → 302 `redirect_uri?code=…&state=…`
3a. **Nothing grantable** (no requested scope held, or `scope` absent) → 302 `redirect_uri?error=access_denied`
4. **Code exchange**:
   - a code plus the matching `code_verifier` → token (the code is now spent)
   - a missing or mismatched verifier, or a spent code → `invalid_grant`
   - a spent code also revokes the tokens issued from it
   - an expired code (after 5 min) → `invalid_grant`, and the application restarts the sign-in
