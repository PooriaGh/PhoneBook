# Feature Specification: Production Readiness: Pagination, Rate Limiting, Observability, End-User Sign-In

**Feature Branch**: `002-production-readiness`

**Created**: 2026-09-25

**Status**: Draft

**Input**: User description: "Pagination for large tag results, rate limiting, OpenTelemetry traces and metrics, and authorization code + PKCE once end users exist."

> **Context**: Builds on feature `001-phonebook-management`, which is delivered and converged. These are the four
> items that feature's README lists under "Trade-offs and future work". The existing behaviour of the phone book
> (add, edit, find by tag, delete, duplicate rule, versions, per-field errors, client-application access) must keep
> working unchanged unless this spec says otherwise.

## Clarifications

### Session 2026-09-25

- Q: What language should the new sign-in page and its messages use? → A: English only, left-to-right, matching the
  API's existing error messages.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Page through large tag results (Priority: P1)

A client application searches for a tag that thousands of contacts share. Instead of receiving every matching
contact at once, it receives a manageable page of results. It is also told how many matches exist in total and
how to fetch the next page, so it can show the results a page at a time.

**Why this priority**: The tag search is currently unbounded. With a large phone book, a single request can
return tens of thousands of entries. That is slow for the caller and costly for the service, and it is the most
likely cause of problems in real use.

**Independent Test**: Seed 250 contacts under one tag. Request the first page with 100 per page, and confirm
that it holds 100 contacts, reports a total of 250, and says more pages exist. Request the third page and
confirm that it holds the last 50 contacts and says no more pages exist.

**Acceptance Scenarios**:

1. **Given** 250 contacts share the tag "همکار", **When** the client requests page 1 with a page size of 100,
   **Then** it receives 100 contacts in the established order (last name, then first name, by character code),
   with total count 250, page 1, page size 100, and an indication that a next page exists.
2. **Given** the same data, **When** the client requests page 3 with a page size of 100, **Then** it receives
   the remaining 50 contacts, and the response indicates that no next page exists.
3. **Given** any data, **When** the client requests a tag search without paging parameters, **Then** it receives
   the first page using the default page size.
4. **Given** 30 matching contacts, **When** the client requests page 5 with a page size of 20, **Then** it
   receives an empty page that still reports the total (30). This is not an error.
5. **Given** any data, **When** the client requests a page number below 1, or a page size below 1 or above the
   maximum, **Then** the request is rejected with a validation error that names the invalid parameter.
6. **Given** a tag with no matching contacts, **When** the client requests page 1, **Then** it receives an empty
   page with a total count of 0.

---

### User Story 2 - Protect the service from excessive use (Priority: P1)

The service limits how many requests one caller can make in a given time window, so that one misbehaving or
compromised client cannot degrade the service for everyone else. Callers over the limit get a clear
"too many requests" answer that says when they may retry. Well-behaved callers are unaffected.

**Why this priority**: The API is public-facing and the token endpoint accepts client secrets. Without limits,
one client can exhaust resources or keep guessing credentials.

**Independent Test**: Send requests from one client faster than its limit allows. Confirm that the requests over
the limit are rejected with a "too many requests" response that includes a retry delay, and that a second
client's requests in the same window still succeed.

**Acceptance Scenarios**:

1. **Given** a client that has used up its allowance for the current window, **When** it sends another request,
   **Then** the request is rejected with a "too many requests" status, a structured error in the service's usual
   error format, and an indication of how long to wait before retrying.
2. **Given** client A is being limited, **When** client B sends requests within its own allowance, **Then** B's
   requests succeed. Limits are tracked per caller, not globally.
3. **Given** a caller that was limited, **When** the retry delay has passed, **Then** its requests succeed again.
4. **Given** repeated token requests from one source, **When** they exceed the token endpoint's stricter
   allowance, **Then** further token requests are rejected with "too many requests" in the token endpoint's
   standard error format.
5. **Given** health checks from an orchestrator, **When** they are called at any rate, **Then** they are never
   limited.

---

### User Story 3 - Operators can observe the running service (Priority: P2)

An operator investigating a slow or failing request can follow the request through the phone book service,
including any calls it makes to the identity service, see where time was spent (including database access), and
correlate it with the error
response the client received. The operator can also watch live service metrics: request rate, latency, error
rate, rejections by the rate limiter and phone book activity (contacts created, updated and deleted).

**Why this priority**: Operations need this, but it does not change behaviour for API users. Stories 1 and 2
protect users directly; observability supports the people running the service.

**Independent Test**: Send a request that creates a contact. Confirm that one connected trace records the
incoming request, the command processing and the database write. Confirm that the trace identifier matches the
`traceId` in any error response, and that the request and domain metrics went up.

**Acceptance Scenarios**:

1. **Given** the service is running with telemetry export enabled, **When** a client creates a contact,
   **Then** one trace connects the incoming HTTP request, the processing of the command and the database
   operations.
2. **Given** a request fails with an error response, **When** the operator looks up that response's trace
   identifier, **Then** it matches the trace recorded for the request.
3. **Given** normal traffic, **When** the operator views metrics, **Then** request count, request duration,
   error count by status, rate-limit rejections and counts of contacts created, updated and deleted are available.
4. **Given** telemetry export is disabled or the telemetry destination is unreachable, **When** clients use the
   API, **Then** every request behaves exactly as it does with telemetry working, with no errors and no
   noticeable slow-down.
5. **Given** any request, **When** its telemetry is recorded, **Then** no personal data (names, phone numbers,
   tags, tokens or secrets) appears in trace attributes, metric labels or logs derived from telemetry.

---

### User Story 4 - End users sign in to use the phone book (Priority: P3)

A person, not just a client application, signs in through the identity service in a browser, using the
standard secure sign-in flow for public clients (authorization code with proof key). They then use the phone book
with read and write permissions granted to them. Machine-to-machine clients keep working unchanged.

**Why this priority**: Feature 001 has no end users; access is for client applications only. This story
prepares the service for when people use it directly (for example from a browser app or the interactive API
documentation). It matters less than protecting the existing service.

**Independent Test**: In a browser, start sign-in from the interactive API documentation, sign in as a seeded
test user, and receive a token. Use it to add and find a contact. Confirm that a request using the
same flow without the proof key is refused.

**Acceptance Scenarios**:

1. **Given** a registered end user, **When** they start sign-in from a browser application and enter valid
   credentials, **Then** the application receives a token that lets the user read and write contacts, according
   to the permissions the user holds.
2. **Given** a sign-in attempt, **When** the proof key is missing or does not match, **Then** no token is issued.
3. **Given** a user who enters wrong credentials, **When** they submit the sign-in form, **Then** they see a
   generic "invalid credentials" message that does not reveal whether the account exists.
4. **Given** an end-user token, **When** the user calls the phone book, **Then** access is granted or refused by
   exactly the same read/write permission rules that apply to client applications.
5. **Given** existing client-credentials clients, **When** they request tokens and call the API, **Then** they
   work exactly as before.
6. **Given** the service is configured with seeded test users, **When** someone tries to create an account,
   **Then** there is no way to do so. Accounts exist only through configuration (no self-registration, no
   external provider). *(Clarified 2026-09-25.)*

---

### Edge Cases

- **Data changes between page requests**: if contacts are added or deleted while a client is paging, a
  contact may appear on two pages or be skipped. This is acceptable for this feature and is documented. Pages
  reflect the data at the moment each page is requested.
- **Very large page numbers**: a page number far beyond the data returns an empty page with the correct total,
  never an error.
- **Paging parameters on a blank or missing tag**: the existing validation for the tag (required, at most 50
  characters) still applies and is reported alongside any paging validation errors.
- **Rate-limit identity**: authenticated requests are counted per client application or per end user.
  Unauthenticated requests (for example to the token endpoint) are counted per source address.
- **Limit reached mid-burst**: requests over the allowance are rejected at once, not queued for later.
- **Rate limiting and errors**: rejected requests still get a trace and still count towards the rate-limit
  rejection metric.
- **Telemetry destination down**: telemetry is dropped quietly and never affects API responses or latency
  beyond a negligible amount.
- **End user without write permission**: gets the same "forbidden" response as a read-only client application.
- **Sign-in replay**: an authorization code can be used only once. A second use is refused, and any token
  already issued from that code is revoked (FR-019).
- **Expired or abandoned sign-in**: an authorization code that is not redeemed within its lifetime (FR-026) is
  refused, and the application must start the sign-in again. If the sign-in session expires between signing in and
  returning to the application, the person is asked to sign in again. Nothing is half-granted.
- **Unknown application or return address**: a sign-in request from an unregistered application, or with a return
  address that is not registered for that application, ends on an error page of the identity service. The person
  is never sent to the unregistered address (FR-024).
- **User without any requested permission**: if none of the requested permissions is held by the user, or none
  was requested, no token is issued and the application receives an "access denied" result (FR-020).
- **Rate limit versus authentication**: a request over the allowance is answered "too many requests" even if it
  would otherwise have been refused as unauthenticated or forbidden (FR-007).

## Requirements *(mandatory)*

### Functional Requirements

**Pagination**

- **FR-001**: The system MUST return tag search results in pages. Each response MUST include the contacts on the
  page, the page number, the page size, the total number of matching contacts and whether a next page exists.
- **FR-002**: The system MUST accept optional page-number and page-size parameters. The default page number is
  1, the default page size is 50 and the maximum page size is 200.
- **FR-003**: The system MUST reject a page number below 1, and a page size below 1 or above the maximum, with a
  per-field validation error in the existing error format.
- **FR-004**: The system MUST keep the established ordering across pages: by last name, then first name,
  compared by Unicode code point (character code), with a stable per-contact tie-break for identical names. Walking
  through every page MUST return each matching contact exactly once when the data does not change. *(Clarified 2026-09-25, security & API checklist.)*
- **FR-005**: The system MUST return an empty page, not an error, when the requested page is beyond the last
  page. The total count is still reported.
- **FR-006**: The paged response MUST replace the current unpaged list response of the existing tag search in
  place, in the same API version. This is a **deliberate breaking change** for existing callers (clarified
  2026-09-25). The response becomes an object holding the page of contacts plus the paging information in
  FR-001. The change MUST be recorded in the API contract and the README as breaking. The README MUST show the
  response before and after the change, so existing callers can migrate.

**Rate limiting**

- **FR-007**: The system MUST limit the number of requests each caller can make to the phone book API per time
  window. Callers are identified by client application or end user when authenticated, otherwise by source
  address. The default allowance is 100 requests per minute per caller. A request over the allowance MUST be
  answered "too many requests" even if it would otherwise be refused as unauthenticated or forbidden. *(Clarified 2026-09-25, security & API checklist.)*
- **FR-008**: The system MUST apply a stricter limit, per source address, to the identity service's
  credential-accepting endpoints: the token endpoint **and** the sign-in form submission. They share one allowance
  per address. The default is 10 requests per minute. Accounts are **not** locked after failed attempts, so an
  attacker cannot lock real users out (clarified 2026-09-25).
- **FR-009**: The system MUST reject requests over the limit with a "too many requests" status and a
  retry-after indication, given in whole seconds and never less than 1. No other rate-limit information (such as
  the remaining allowance) is returned. The response depends on who is asking:
  - **Phone book API**: the existing structured error format, with a distinct error code.
  - **Token endpoint**: the endpoint's usual error-body shape (an error code and a description). The error code
    used for this case MUST be named in the identity contract.
  - **Sign-in form**: the person sees the sign-in page again, with the message "Too many attempts. Try again in N
    seconds." They never see a raw machine-readable error.

  *(Clarified 2026-09-25, security & API checklist.)*
- **FR-010**: The system MUST NOT rate-limit health-check endpoints, the interactive API documentation, or the
  identity service's public metadata (its discovery document and signing keys).
- **FR-011**: The system MUST make the rate-limit allowances and window lengths configurable without code
  changes.

**Observability**

- **FR-012**: The system MUST record a distributed trace for every request to either service. A phone book trace
  MUST cover the incoming request, command or query processing and database access. When the phone book service
  calls the identity service (for example to fetch its signing keys), that call and the identity service's handling
  of it MUST be recorded in **one** trace. Requests that clients send separately to each service are separate
  traces. *(Clarified 2026-09-25, after `/speckit-analyze` finding G1.)*
- **FR-013**: The trace identifier included in error responses MUST be the identifier of the recorded trace, in
  the standard trace-identifier form of the telemetry standard. The change of format from feature 001 MUST be
  recorded in the API contract.
- **FR-014**: The system MUST publish metrics for:
  - request count and duration, by endpoint and status
  - rate-limit rejections
  - contacts created, updated and deleted
- **FR-015**: The system MUST export traces and metrics to a configurable destination using an open, vendor-
  neutral telemetry standard. Export MUST be possible to disable through configuration.
- **FR-016**: The system MUST NOT include personal or secret data in telemetry **or in the services' logs**:
  - no contact names, phone numbers or tag values
  - no end-user user names or passwords
  - no client secrets or tokens
  - no client network addresses

  Request bodies MUST never be recorded. *(Clarified 2026-09-25, security & API checklist.)*
- **FR-017**: A failure or slowness of the telemetry destination MUST NOT cause request failures or noticeable
  added latency.

**End-user sign-in**

- **FR-018**: The identity service MUST support the authorization code flow with a mandatory proof key (PKCE)
  for public browser-based clients. Only the hashed proof-key method is accepted. The plain method is refused.
- **FR-019**: The identity service MUST refuse token requests whose proof key is missing or does not match, and
  MUST allow each authorization code to be used only once. When a used code is presented again, tokens already
  issued from it MUST be revoked.
- **FR-020**: End-user tokens MUST carry the same read and write permissions as client-application tokens. The
  phone book API MUST enforce them with the existing rules and no new permission model. The permissions granted
  are those requested that the user holds. A requested permission the user does not hold is left out without an
  error, which is standard OAuth down-scoping. If no requested permission remains, or none was requested, the
  sign-in ends with "access denied" and no token. *(Clarified 2026-09-25, security & API checklist.)*
- **FR-021**: Sign-in failures MUST show a generic message that does not reveal whether an account exists. The
  response time for an unknown account MUST be comparable to that for a wrong password, so timing does not reveal
  it either.
- **FR-022**: The interactive API documentation MUST let a person sign in with this flow, in addition to the
  existing client-credentials option.
- **FR-023**: Existing client-credentials behaviour MUST remain unchanged.

**Sign-in security** *(Clarified 2026-09-25, security & API checklist.)*

- **FR-024**: The identity service MUST return a person to an application only at a return address that exactly
  matches one registered for that application. After signing in, it MUST continue only to addresses within the
  identity service itself. An unregistered application or return address ends on an error page.
- **FR-025**: The sign-in form MUST be protected against cross-site request forgery. The sign-in session MUST NOT
  be readable by scripts, MUST NOT be sent on cross-site sub-requests, and MUST be marked secure when served over
  HTTPS.
- **FR-026**: Lifetimes:
  - an authorization code: at most 5 minutes
  - an end-user access token: 1 hour, the same as client-application tokens
  - the sign-in session: 15 minutes

  Sign-out is out of scope: the session and tokens simply expire (clarified 2026-09-25).
- **FR-027**: Seeded end users MUST exist only in development configuration. Every other environment starts
  with none. Passwords MUST be stored only as salted hashes, using a deliberately slow scheme that meets current
  industry guidance (for example the OWASP password-storage recommendations).

### Key Entities

- **Page of contacts**: the contacts on one page of a tag search, together with the page number, page size,
  total matching count and whether a next page exists.
- **Rate-limit policy**: the allowance (number of requests), the window length and how callers are identified,
  for one class of endpoint (phone book API or token endpoint).
- **Trace**: the recorded path of one request through the phone book and identity services, identified by the
  trace identifier that clients see in error responses.
- **End user**: a person who can sign in, holding read and/or write permission on the phone book.
- **Public client**: a browser-based application (for example the interactive API documentation) that signs
  users in with the authorization code flow and a proof key. It has no client secret.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With 100,000 contacts under one tag, any single page of results is returned in under 500 ms.
- **SC-002**: Walking through every page of a 250-contact tag, with the data unchanged, returns each contact
  exactly once, in the established order.
- **SC-003**: When one client sends 10 times its allowance, 100% of its excess requests get "too many requests"
  with a retry delay, while a second client within its own allowance sees a 0% rejection rate.
- **SC-004**: 100% of API requests produce a trace whose identifier matches the `traceId` in any error response
  for that request.
- **SC-005**: With the telemetry destination unreachable, API error rates and median response times stay within
  5% of the values measured with telemetry working.
- **SC-006**: An automated scan of exported telemetry during a full test run finds no names, phone numbers,
  tag values, tokens or secrets.
- **SC-007**: A person can sign in through the interactive API documentation and add a contact in under
  2 minutes, and a sign-in attempt without a valid proof key never yields a token.
- **SC-008**: All existing feature-001 acceptance tests still pass. The only exception is the tag-search tests,
  which are updated to the new paged response shape (FR-006, a deliberate breaking change). Their expected
  contacts and order stay the same.

## Assumptions

- **Page-number paging**: paging uses page numbers and a page size, not continuation tokens. That is simple for
  clients and enough at this scale, and the stable character-code ordering makes pages deterministic.
- **Default numbers**: page size 50 (maximum 200), API limit 100 requests per minute per caller, token endpoint
  limit 10 per minute per source address. All can be changed by configuration (FR-011).
- **Rate-limit state is per instance**: counters live in each service instance's memory, in line with the
  in-memory design of feature 001. Coordinating limits across several instances is out of scope.
- **Telemetry destination**: an operator-provided collector. A local development setup (for example a container
  in the existing compose file) is enough to demonstrate traces and metrics. Choosing and hosting a monitoring
  back end is out of scope.
- **End users come from configuration** (clarified 2026-09-25): a small set of test users with read and/or write
  permission is seeded from configuration. Passwords are stored only as hashes at runtime, and plain-text values
  appear only in development configuration marked DEV-ONLY. Out of scope: self-registration, external identity
  providers, account recovery, multi-factor authentication and user management screens.
- **No sign-out and no account lockout** (clarified 2026-09-25): sign-out and per-account lockout are out of
  scope. Short lifetimes (FR-026) and the per-address limit (FR-008) cover the risk for this feature.
- **Source address behind a proxy**: the source address used for rate limiting is the address of the direct
  connection. A deployment behind a reverse proxy must configure trusted forwarded-address handling, or all callers
  share one allowance. This is documented as a deployment note.
- **Sign-in page language** (clarified 2026-09-25): the sign-in page and its messages are in English, laid out
  left to right, matching the service's existing error messages. The exact texts are "Invalid username or
  password." and "Too many attempts. Try again in N seconds." Contact data may still be Persian, since the page
  never displays it. Localisation is out of scope.
- **First-party client, no consent screen**: the interactive API documentation is a first-party client, so
  signed-in users are not shown a separate consent screen.
- **Delivery constraints**:
  - These are carried over from feature 001 and the constitution, and are recorded here for planning.
  - The user named "OpenTelemetry" for traces and metrics and "authorization code + PKCE" for end-user sign-in.
  - Every new error response from the phone book API follows the existing structured error format (constitution
    Principle III).
  - Tests follow constitution Principle V: tests first, integration tests on both database providers.
