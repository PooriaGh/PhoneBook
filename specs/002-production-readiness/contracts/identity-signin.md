# Contract: End-user sign-in and rate limiting (PhoneBook.Identity)

This is a delta to [`../../001-phonebook-management/contracts/identity-token.md`](../../001-phonebook-management/contracts/identity-token.md).
The client-credentials behaviour there is unchanged (FR-023).

## Discovery additions

`/.well-known/openid-configuration` now also advertises:
- `authorization_endpoint` = `{issuer}connect/authorize`
- `grant_types_supported` ⊇ `client_credentials`, `authorization_code`
- `response_types_supported` ⊇ `code`
- `code_challenge_methods_supported` = `["S256"]` (**only** S256; `plain` is refused, FR-018)

## Authorization endpoint: `GET /connect/authorize`

| Parameter | Required | Value |
|---|---|---|
| `client_id` | yes | `phonebook-swagger-ui` |
| `response_type` | yes | `code` |
| `redirect_uri` | yes | a registered URI (for example `https://localhost:7001/swagger/oauth2-redirect.html`) |
| `scope` | yes | subset of `phonebook.read phonebook.write` |
| `state` | recommended | opaque value |
| `code_challenge` | **yes** | BASE64URL(SHA256(code_verifier)) |
| `code_challenge_method` | **yes** | `S256` |

| Situation | Result |
|---|---|
| Not signed in | `302` → `/account/login?ReturnUrl=<authorize URL>` |
| Signed in | `302` → `redirect_uri?code=…&state=…`; consent is implicit for this first-party client |
| Missing `code_challenge`, or `code_challenge_method=plain` | `302` → `redirect_uri?error=invalid_request…` (PKCE with S256 is required) |
| No requested scope is held by the user, or `scope` is absent | `302` → `redirect_uri?error=access_denied&state=…` (FR-020) |
| Unknown `client_id`, or a `redirect_uri` that is not an **exact** match for a registered one | `400` `invalid_request` rendered by the Identity host itself; **never** redirects (FR-024) |

Granted scopes are the requested scopes **intersected with the user's configured scopes**. For example, `bob`
asking for write access receives read only.

## Login: `GET /account/login`, `POST /account/login`

- The page is English only, left to right (`lang="en" dir="ltr"`), with a labelled field for each input (spec Clarifications).
- `GET` returns an HTML form (`username`, `password`, antiforgery token, hidden `ReturnUrl`).
- `POST` results:
  - **Valid credentials:** a cookie is set, then `302` → `ReturnUrl`. Only local URLs are allowed, which prevents
    open redirects.
  - **Invalid credentials:** `200` with the same form and the message **"Invalid username or password."**,
    identical for unknown users and wrong passwords (FR-021).
  - **Missing or invalid antiforgery token:** `400`.
  - **Over the rate limit:** `429`, `Retry-After`, and the sign-in page with "Too many attempts. Try again in N
    seconds." Always an HTML page, never JSON (FR-009).
- Rate limited by the `token` policy (per IP, shared with `/connect/token`). There is no per-account lockout.
- Response time for an unknown user is comparable to a wrong password (FR-021).
- **The cookie** is `HttpOnly`, `SameSite=Lax`, Secure over HTTPS, and lasts 15 minutes without sliding (FR-025,
  FR-026). There is no sign-out endpoint (out of scope).

## Token endpoint: `POST /connect/token` with `grant_type=authorization_code`

| Field | Required |
|---|---|
| `grant_type=authorization_code` | yes |
| `client_id=phonebook-swagger-ui` | yes (public client, no secret) |
| `code` | yes |
| `redirect_uri` | yes (must match) |
| `code_verifier` | **yes** |

| Case | Result |
|---|---|
| Valid code and verifier | `200` `{access_token, token_type: "Bearer", expires_in, scope}`. The JWT has `sub` = user id, `name`, `aud` = `phonebook-api` and the granted `scope` |
| Missing `code_verifier` | `400` `invalid_grant`. The test records the observed code, and this row is corrected if it differs (CHK015) |
| Mismatched `code_verifier` | `400` `invalid_grant` |
| Code already redeemed | `400` `invalid_grant`, **and** the tokens issued from that code are revoked (FR-019) |
| Code older than 5 minutes | `400` `invalid_grant` (FR-026) |

Access tokens last **1 hour**, the same as client credentials.

## Rate limiting (both token grant types; the login POST is described above)

Over the limit: `429`, a `Retry-After` header (whole seconds, at least 1), and the body
`{"error":"temporarily_unavailable","error_description":"Too many requests. Retry later."}`.

**Documented extension (CHK018)**: RFC 6749 defines no token-endpoint error for throttling. `temporarily_unavailable`
(RFC 6749 §4.1.2.1) is the closest standard code. Clients should rely on the HTTP `429` status and `Retry-After`.

Not limited: `/.well-known/openid-configuration`, the JWKS endpoint and `/health/*` (FR-010).

## Seeded users (DEV-ONLY, `appsettings.Development.json`)

| User name | Password | Scopes |
|---|---|---|
| `alice` | `alice-dev-password` | `phonebook.read`, `phonebook.write` |
| `bob` | `bob-dev-password` | `phonebook.read` |

Seeded users exist **only** in the Development environment. `appsettings.json` has none (FR-027). Passwords are
held only as PBKDF2 hashes (research R-08).
