# Quickstart & Validation Guide: Phone Book Management

**Feature**: `001-phonebook-management` | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

This guide proves the feature works from end to end. Endpoint shapes are defined in [phonebook-api.openapi.yaml](./contracts/phonebook-api.openapi.yaml) and [identity-token.md](./contracts/identity-token.md). Business rules are defined in [data-model.md](./data-model.md).

## Prerequisites

- .NET SDK 10.0.x (`dotnet --version`)
- Docker Desktop **running**. It is required only for integration tests (Testcontainers) and for the optional compose profile. The app itself runs with no Docker, on in-memory SQLite.
- A trusted dev HTTPS certificate: `dotnet dev-certs https --trust`

## 1. Build and run all tests

```bash
dotnet build PhoneBook.slnx -c Release
```

```bash
dotnet test PhoneBook.slnx -c Release
```

**Expected**: the build has no warnings, because warnings are treated as errors, and every test passes:
- Domain unit tests, which need no Docker
- Architecture tests
- API integration tests, where one PostgreSQL container starts and Respawn resets it between tests
- Identity integration tests, including the end-to-end token → API test

## 2. Run the two hosts locally (in-memory mode)

Terminal 1:

```bash
dotnet run --project src/PhoneBook.Identity --launch-profile https
```

Terminal 2:

```bash
dotnet run --project src/PhoneBook.Api --launch-profile https
```

**Expected**:
- Identity listens on `https://localhost:7002`
- The API listens on `https://localhost:7001`
- `GET /health/ready` on both returns `200 Healthy`

## 3. Validate through Swagger (SC-001)

1. Open `https://localhost:7001/swagger`.
2. Click **Authorize**, select both scopes, and use client `phonebook-swagger` with the dev secret from `src/PhoneBook.Identity/appsettings.Development.json`.
   *Note (feature 002):* Swagger UI now pre-fills `phonebook-swagger-ui` for the new sign-in flow, so for client credentials type `phonebook-swagger` yourself.
3. Walk through the scenarios below and check each result.

| # | Action | Expected (spec ref) |
|---|---|---|
| 1 | `POST /api/v1/contacts` `{ "firstName":"علی","lastName":"رضایی","phoneNumber":"+98 912 123 4567","tag":"همکار" }` | `201`, `Location` and `ETag: "1"` headers, `phoneNumber` = `+989121234567` (US1-1) |
| 2 | Same request again | `409 Contact.Duplicate` (FR-018) |
| 3 | Same request with `"tag":"دوست"` | `201`. The same number is allowed under a different tag. |
| 4 | `POST` with `phoneNumber:"۰۹۱۲۱۲۳۴۵۶۷"` (Persian digits) and a new tag | `201`, stored as `09121234567` (edge case) |
| 5 | `POST` with `lastName:""` and `phoneNumber:"abc"` | `400` ValidationProblemDetails with errors for `lastName` **and** `phoneNumber` (US1-2, US1-3) |
| 6 | `GET /api/v1/contacts?tag=%20همکار%20` | `200`, returns the contact from step 1 (US2-1, US2-3) |
| 7 | `GET /api/v1/contacts?tag=unknown` | `200 []` (US2-2) |
| 8 | `GET /api/v1/contacts?tag=` | `400` (US2-4) |
| 9 | `PUT /api/v1/contacts/{id from 1}` with the tag changed to `دوست2`, header `If-Match: "1"` | `200`, `version: 2`, `ETag: "2"` (US3-1) |
| 10 | Repeat step 9 with `If-Match: "1"` | `412` (concurrency) |
| 11 | `GET ?tag=همکار` then `GET ?tag=دوست2` | Not found under the old tag, found under the new tag (US3-2) |
| 12 | `PUT /api/v1/contacts/{random guid}` | `404` (US3-3) |
| 13 | `GET /api/v1/contacts/not-a-guid` (authorized) | `400 Contact.Id.Invalid` (edge case). Called anonymously, it returns `401`. |
| 14 | `DELETE /api/v1/contacts/{id}` | `204`. Repeating it returns `404` (US4-1, US4-2). |
| 15 | Log out, then call any endpoint | `401` with a `application/problem+json` body containing `errorCode: Auth.Unauthorized` |
| 16 | Authorize as `phonebook-readonly` and `POST` | `403` ProblemDetails with `errorCode: Auth.Forbidden` |
| 17 | Add contacts with last names `رضایی` and `احمدی` under one tag, then search that tag | `احمدی` comes first (ordinal order, the same on both providers) |

## 4. Token via command line (optional)

```bash
curl -s -X POST https://localhost:7002/connect/token -d "grant_type=client_credentials&client_id=phonebook-swagger&client_secret=<dev-secret>&scope=phonebook.read phonebook.write"
```

**Expected**: JSON containing `access_token`, `token_type: "Bearer"` and `expires_in`.

## 5. Docker Compose (optional, PostgreSQL provider)

```bash
docker compose --profile postgres up --build
```

**Expected**:
- The API and Identity containers start.
- The API uses `Database__Provider=Postgres`.
- Scenario table §3 behaves the same way.

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| Integration tests fail with "Docker is either not running…" | Start Docker Desktop |
| Swagger "Authorize" fails with a CORS error | Add the API origin to `Identity:AllowedCorsOrigins` |
| `401` even with a token | Identity must be running *before* the first API call (the API reads the discovery document). Check that `Auth:Authority` matches the Identity URL. |
| Data disappeared | Expected: the default provider is in-memory (spec FR-016) |
