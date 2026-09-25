# Feature Specification: Phone Book Management

**Feature Branch**: `001-phonebook-management`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "All features are described in docs/documentation.pdf (in Persian language). Build the specification from that document."

> **Source summary (translated from the Persian brief in `docs/documentation.pdf`)**: Build a phone book that supports:
> (1) adding an entry with first name, last name, phone number and a specific tag (e.g. "my colleague's number at Taraborent"),
> (2) editing an existing entry,
> (3) retrieving all entries that carry a given tag, and
> (4) deleting an entry.
> Data does not need to be persisted; everything may be kept in memory. All operations must be exposed as resource-oriented web endpoints with interactive, browsable documentation. The project must include tests and a cover letter describing the architecture and the implementation process.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a contact to the phone book (Priority: P1)

A phone book user records a new contact by giving a first name, last name, phone number and one descriptive tag (for example "شماره همکارم در ترابرنت" / "my colleague's number at Taraborent"). The system stores the entry, assigns it a unique identifier, and returns the saved entry.

**Why this priority**: Nothing else in the phone book is possible without entries. Adding an entry is the minimum viable product.

**Independent Test**: Add an entry with valid data and confirm the response contains the new entry with its identifier and all given values.

**Acceptance Scenarios**:

1. **Given** an empty phone book, **When** the user adds an entry with first name "علی", last name "رضایی", phone number "+989121234567" and tag "همکار", **Then** the entry is saved, a unique identifier is returned, and the returned entry shows the given names and tag, and the phone number in normalized form.
2. **Given** any phone book, **When** the user adds an entry with the first name, last name, phone number or tag missing or blank, **Then** the entry is rejected with a validation message naming each invalid field, and nothing is stored.
3. **Given** any phone book, **When** the user adds an entry whose phone number contains letters or other disallowed characters, **Then** the entry is rejected with a validation message about the phone number.

---

### User Story 2 - Find all contacts with a given tag (Priority: P1)

The user wants to see every contact that shares a tag (for example, all numbers tagged "همکار"). They supply a tag and receive the list of all entries that carry it.

**Why this priority**: Tag-based lookup is the only retrieval feature the brief requires. Together with adding entries, it makes the phone book usable.

**Independent Test**: Add several entries with different tags, request one tag, and confirm that exactly the matching entries come back.

**Acceptance Scenarios**:

1. **Given** three entries tagged "همکار" and two tagged "خانواده", **When** the user requests entries tagged "همکار", **Then** exactly the three matching entries are returned.
2. **Given** entries exist but none carry the tag "دوست", **When** the user requests entries tagged "دوست", **Then** an empty list is returned. This is a normal result, not an error.
3. **Given** an entry tagged "Work", **When** the user requests the tag " work " (different letter case and surrounding spaces), **Then** that entry is returned.
4. **Given** any phone book, **When** the user requests entries with a blank tag, **Then** the request is rejected with a validation message.

---

### User Story 3 - Edit an existing contact (Priority: P2)

The user corrects or updates a contact's details, such as a changed phone number, a misspelled name or a different tag, by identifying the entry and giving its new values.

**Why this priority**: Details change over time, but the phone book is still useful without editing because a user can delete an entry and add it again.

**Independent Test**: Add an entry, edit it, then look it up by its new tag and confirm the updated values are shown.

**Acceptance Scenarios**:

1. **Given** an existing entry, **When** the user sends new valid values for first name, last name, phone number and tag, **Then** the entry is updated, keeps the same identifier, and the updated entry is returned.
2. **Given** an entry tagged "همکار" that is edited to the tag "دوست", **When** the user then searches for "همکار", **Then** the edited entry no longer appears. **And When** the user searches for "دوست", **Then** it does appear.
3. **Given** no entry with the given identifier, **When** the user tries to edit it, **Then** the system responds with "not found" and nothing changes.
4. **Given** an existing entry, **When** the user sends invalid values (for example a blank last name), **Then** the edit is rejected with validation messages and the stored entry is unchanged.

---

### User Story 4 - Delete a contact (Priority: P2)

The user removes a contact that is no longer needed.

**Why this priority**: Removing entries keeps the phone book accurate, but it matters less than creating and finding entries.

**Independent Test**: Add an entry, delete it, and confirm it no longer appears in tag searches and cannot be edited.

**Acceptance Scenarios**:

1. **Given** an existing entry, **When** the user deletes it by its identifier, **Then** the deletion is confirmed and the entry no longer appears in any tag search.
2. **Given** no entry with the given identifier (including one that was already deleted), **When** the user tries to delete it, **Then** the system responds with "not found".

---

### Edge Cases

- **Persian and other Unicode text**: Names and tags may contain Persian or other non-Latin characters, including the zero-width non-joiner. They are stored and returned exactly as given, apart from trimming leading and trailing spaces.
- **Tag matching**: Leading and trailing spaces are ignored and letter case is ignored. Only exact tag matches count, so "همکار" does not match "همکاران".
- **Phone number formatting**: A number may start with an optional "+" and may contain spaces or hyphens as visual separators. Other characters are rejected. The number must contain 4–15 digits.
- **Persian/Arabic-Indic digits**: A phone number typed with Persian digits (۰–۹) or Arabic-Indic digits is accepted and treated as the same number written with Western digits.
- **Duplicate phone numbers**: The same phone number may appear on several entries **with different tags**, for example a shared office line tagged "همکار" and "دفتر". Adding the same phone number a second time under the **same tag** (compared ignoring letter case and spaces) is rejected as a duplicate conflict. This applies to both adding and editing.
- **Overlong input**: A value longer than its maximum length is rejected with a validation message. The limits are 100 characters for each name and 50 characters for the tag.
- **Malformed identifier**: A request with an identifier in an invalid format is rejected as a bad request instead of being treated as "not found".
- **Simultaneous changes**: When requests to add, edit or delete arrive at the same time, the phone book is never left inconsistent. For example, an entry is never half-updated.
- **Restart**: Because data is kept only in memory, all entries are lost when the service restarts. This is expected behaviour.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a user to add a phone book entry with a first name, last name, phone number and exactly one tag.
- **FR-002**: The system MUST give each new entry a unique, stable identifier and return it together with the saved entry.
- **FR-003**: The system MUST reject an entry if any required field (first name, last name, phone number or tag) is missing or blank after trimming spaces.
- **FR-004**: The system MUST validate phone numbers: an optional leading "+", then digits with optional space or hyphen separators, with 4–15 digits in total. Persian and Arabic-Indic digits MUST be accepted and converted to Western digits. The phone number is stored and returned in normalized form: an optional leading "+" followed by digits only.
- **FR-005**: The system MUST enforce maximum lengths of 100 characters for the first name, 100 for the last name and 50 for the tag.
- **FR-006**: The system MUST allow a user to replace all editable fields (first name, last name, phone number and tag) of an existing entry, identified by its identifier. The same validation rules as for adding MUST apply.
- **FR-007**: The system MUST keep an entry's identifier unchanged when the entry is edited.
- **FR-008**: The system MUST allow a user to retrieve every entry that carries a given tag. Matching MUST be exact, MUST ignore letter case and MUST ignore leading and trailing spaces. Results MUST be ordered by last name, then first name, comparing by character code, so the order is the same in every environment.
- **FR-009**: The system MUST return an empty list, not an error, when no entry matches the requested tag.
- **FR-010**: The system MUST allow a user to delete an entry by its identifier. After deletion, the entry MUST NOT appear in any later result.
- **FR-011**: The system MUST respond with a distinct "not found" outcome when a user edits or deletes an identifier that does not exist.
- **FR-012**: The system MUST respond to invalid input with a clear, structured error that lists each invalid field and the reason it is invalid.
- **FR-013**: The system MUST store names and tags exactly as given, apart from trimming spaces, with full Unicode support including Persian text.
- **FR-014**: The system MUST expose every operation as a resource-oriented web endpoint that uses standard methods and status codes.
- **FR-015**: The system MUST provide interactive, browsable documentation of all endpoints, in which each operation can be tried directly.
- **FR-016**: The system MUST keep all data in memory only. Nothing needs to survive a restart.
- **FR-017**: The system MUST remain consistent when requests to add, edit, delete or search arrive at the same time. If an edit or delete is based on an outdated version of an entry, it MUST be rejected and not applied silently. The rejection is a failed precondition when the client supplied the version it last saw, and a conflict otherwise.
- **FR-018**: The system MUST reject adding or editing an entry when another entry already has the same phone number under the same tag (tags compared ignoring letter case and surrounding spaces). The system MUST reply with a distinct "duplicate" conflict.
- **FR-019**: The system MUST allow only authorised client applications to use the phone book. Reading requires read permission. Adding, editing and deleting require write permission.

### Key Entities

- **Phone Book Entry (Contact)**: A single row in the phone book. Attributes: a unique identifier, a first name, a last name, a phone number and a tag. The entry is responsible for enforcing its own rules: fields must not be blank, lengths must stay within their limits, and the phone number must be valid.
- **Phone Number**: A value describing how to reach the contact. Two phone numbers are equal when their normalised digits (with the leading "+" kept) are the same. It has no identity of its own.
- **Tag**: A short free-text label that categorises an entry, such as "همکار" or "شماره همکارم در ترابرنت". Two tags are equal when they match after trimming spaces and ignoring letter case. It has no identity of its own.
- **Phone Book**: The collection of all entries. It supports adding, editing, deleting and finding entries by tag.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can add, edit, find by tag and delete an entry using only the interactive documentation, with no other tools or guidance, in under 5 minutes in total.
- **SC-002**: 100% of the acceptance scenarios in this specification pass in automated tests.
- **SC-003**: Every invalid request tested (missing fields, bad phone numbers, overlong values, unknown identifiers) receives an error that names the problem. None of them cause an unexpected failure.
- **SC-004**: With 10,000 entries in the phone book, a search by tag returns its results in under 1 second.
- **SC-005**: When 100 requests to add, edit, delete and search are made at the same time, no entry is lost, duplicated or corrupted.
- **SC-006**: The project includes a cover letter that explains the architecture and the implementation process. A reviewer can read it and understand the structure of the solution in under 15 minutes.

## Assumptions

- **Single tag per entry**: The brief says "a specific tag" (یک برچسب به خصوص) in the singular, so each entry carries exactly one tag. Supporting several tags per entry is out of scope.
- **Access control**: The brief does not mention security. As a planning decision, access is limited to client applications that hold a token from a separate identity service. Client applications get read and/or write permission. There are no individual end-user accounts.
- **Only the four listed operations are required**: Listing all entries or getting a single entry by identifier are not required by the brief. They may be added only if they help with testing or usability, and they do not change the scope above.
- **Edit replaces the whole entry**: An edit supplies all four fields. Updating individual fields separately is out of scope.
- **Duplicates**: Duplicate names are allowed. Duplicate phone numbers are allowed only under different tags (see FR-018).
- **Delivery constraints from the brief**: These are recorded here for the planning phase:
  - The solution is built on .NET Core (DotNet Core).
  - Domain-Driven Design principles are followed as closely as practical.
  - Storage is in memory only.
  - The endpoints are RESTful and documented through Swagger.
  - The project includes tests, chosen at the developer's discretion.
  - A cover letter describing the architecture and the implementation process is attached to the project.
  - The deadline is 3 days from receiving the brief.
