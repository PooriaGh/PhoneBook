# Quickstart & Validation: Production Readiness

**Feature**: `002-production-readiness`

**Related**: [contracts](./contracts/) · [data-model](./data-model.md) ·
[feature 001 quickstart](../001-phonebook-management/quickstart.md), whose prerequisites still apply

## 1. Automated validation

```bash
dotnet build PhoneBook.slnx -c Release          # 0 warnings
dotnet test --solution PhoneBook.slnx -c Release # Docker running (Testcontainers PostgreSQL with C collation)
```

**Expected**: all feature-001 tests pass, apart from the tag-search tests, which are updated to the paged shape.
The new suites for paging, rate limiting, telemetry and end-user sign-in pass on both providers where relevant.

## 2. Manual scenarios (both hosts running, as in the feature 001 quickstart §2)

| # | Action | Expected (spec ref) |
|---|---|---|
| 1 | Seed 250 contacts with tag `load`, then `GET /api/v1/contacts?tag=load&page=1&pageSize=100` | `200` with `items` holding 100, `totalCount: 250`, `hasNext: true` (US1-1) |
| 2 | Same with `page=3` | 50 items, `hasNext: false` (US1-2) |
| 3 | `GET ?tag=load` with no paging parameters | 50 items (the default page size) (US1-3) |
| 4 | `?tag=load&page=99` | `200`, empty `items`, `totalCount: 250` (US1-4) |
| 5 | `?tag=load&page=0&pageSize=500` | `400` with `errors.page` and `errors.pageSize` (US1-5) |
| 6 | With `RateLimiting:Api:PermitLimit=5`, send 6 GETs quickly with one token | the 6th gets `429`, `Retry-After`, `errorCode: RateLimit.Exceeded` (US2-1) |
| 7 | While limited, call with a token for another client | `200` (US2-2) |
| 8 | Wait for `Retry-After`, then retry | `200` (US2-3) |
| 9 | 11 quick `POST /connect/token` calls from one machine | the 11th gets `429` `temporarily_unavailable` (US2-4) |
| 10 | Hammer `/health/ready` | always `200` (US2-5) |
| 11 | `docker compose --profile observability up`, create a contact, open the Aspire dashboard at `http://localhost:18888` | one trace containing the HTTP request, the `PhoneBook.Application` span and the persistence span. Metrics are visible (US3-1, US3-3) |
| 12 | Cause a `404`, then search the dashboard for the response's `traceId` | the trace is found (US3-2) |
| 13 | Stop the dashboard container and repeat #1 twenty times; compare the median with the same run while the dashboard is up | the same results, and a median within 5% (US3-4, SC-005; research R-04) |
| 14 | Swagger UI → Authorize → **authorizationCode** (client id pre-filled as `phonebook-swagger-ui`) → sign in as `alice` / `alice-dev-password` | signed in with no consent screen; POST and GET succeed (US4-1) |
| 15 | Sign in as `bob` and POST a contact | `403` (US4-4) |
| 16 | Sign in with a wrong password | "Invalid username or password." (US4 AC3) |
| 17 | Submit the sign-in form 11 times quickly with wrong passwords | the 11th shows the sign-in page with "Too many attempts. Try again in N seconds." (FR-009) |
| 18 | Edit the authorize URL to `code_challenge_method=plain`, or change `redirect_uri` to another address | `400 invalid_request` shown by the Identity host; never redirected to the edited address (FR-018, FR-024) |
| 19 | As `bob`, authorize with `scope=phonebook.write` only | the application receives `error=access_denied` (FR-020) |
| 20 | After a successful sign-in, check the cookie in the browser developer tools | `HttpOnly`, `SameSite=Lax`, about 15 minutes' lifetime (FR-025, FR-026) |

**Notes**:
- **Client credentials in Swagger UI**: the pre-filled client id is now the public `phonebook-swagger-ui`. To use
  the clientCredentials flow, type `phonebook-swagger` and its DEV-ONLY secret (research R-05).
- **Paging while data changes**: a contact may appear twice or be skipped across pages. This is expected
  (data-model §1).

## 3. Troubleshooting

| Symptom | Fix |
|---|---|
| Page order differs between SQLite and PostgreSQL | The PostgreSQL database was not created with `LC_COLLATE=C` (research R-02). Recreate it with `POSTGRES_INITDB_ARGS=--locale=C --encoding=UTF8` |
| Integration tests unexpectedly get `429` | The test factory must set high `RateLimiting:*` limits; only the dedicated rate-limit tests use low limits, each with a fresh host (research R-07) |
| All clients share one rate-limit bucket behind a proxy | Configure `ForwardedHeaders` so `RemoteIpAddress` is the client's address (research R-03) |
| No traces in the dashboard | Set `Telemetry__OtlpEndpoint=http://aspire-dashboard:18889` (compose) or `http://localhost:4317` (local) |
