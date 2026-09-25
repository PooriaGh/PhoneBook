# Contract: PhoneBook.Identity (OpenIddict authorization server)

**Base URL (dev)**: `https://localhost:7002`

## Discovery

`GET /.well-known/openid-configuration` → `200` JSON containing at least `issuer`, `token_endpoint`, `jwks_uri`, and `grant_types_supported` including `client_credentials`.

`GET /.well-known/jwks` → `200` JSON Web Key Set with the public signing keys the API uses for validation.

## Token endpoint (OAuth 2.0 client credentials, RFC 6749 §4.4)

`POST /connect/token`, `Content-Type: application/x-www-form-urlencoded`

| Field | Required | Example |
|---|---|---|
| `grant_type` | yes | `client_credentials` |
| `client_id` | yes | `phonebook-swagger` |
| `client_secret` | yes | (from `appsettings.Development.json`, dev-only) |
| `scope` | no | `phonebook.read phonebook.write` |

**200 OK**

```json
{ "access_token": "<JWT>", "token_type": "Bearer", "expires_in": 3600, "scope": "phonebook.read phonebook.write" }
```

The access token is a signed JWT that is **not encrypted**. It contains:
- `iss` = the Identity base URL
- `aud` = `phonebook-api`
- `sub` = `client_id`
- `scope` = the granted scopes

**Errors** (RFC 6749 §5.2):

| Case | Status | `error` |
|---|---|---|
| Unknown client or wrong secret | 401 | `invalid_client` (OpenIddict ID2055) |
| Unsupported grant type | 400 | `unsupported_grant_type` |
| Scope that does not exist | 400 | `invalid_scope` |
| Scope exists but the client has no permission for it | 400 | `invalid_request` (OpenIddict ID2051) |

*Reconciled during implementation (T121): OpenIddict 7.7.1 reports a permitted-but-not-granted scope as `invalid_request`, not `invalid_scope`; verified against the running host.*

## Issuer and CORS

- The issuer is fixed by `Identity:Issuer` (dev: `https://localhost:7002/`; compose: `http://identity:8080/`), so the `iss` claim does not depend on the request host.
- CORS: `POST` and `OPTIONS` on `/connect/token` are allowed from the origins in `Identity:AllowedCorsOrigins` (dev: `https://localhost:7001`, `http://localhost:5001`). A preflight from any other origin gets no `Access-Control-Allow-Origin` header.
- Errors from this host follow RFC 6749 JSON, **not** ProblemDetails (research R-17).

## Seeded clients (development only)

| client_id | Allowed scopes | Purpose |
|---|---|---|
| `phonebook-swagger` | `phonebook.read`, `phonebook.write` | Swagger UI "Authorize" and the reviewer's own calls |
| `phonebook-readonly` | `phonebook.read` | Demonstrates `403` on write endpoints |

## API authorization policies (resource server side)

| Policy | Requirement | Endpoints |
|---|---|---|
| `Contacts.Read` | scope `phonebook.read` | `GET /api/v1/contacts`, `GET /api/v1/contacts/{id}` |
| `Contacts.Write` | scope `phonebook.write` | `POST`, `PUT`, `DELETE` |
