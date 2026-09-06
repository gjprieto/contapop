# Contracts

This document catalogs the commands and queries each service exposes, following the CQRS conventions in `docs/standards/backend-api-code-guidelines.md` (a command changes state, a query reads state, neither type does both) and building directly on `docs/analysis/domain.md`, `docs/analysis/services.md`, and `docs/analysis/events.md`.

**Scope: MVP only.** This is a first pass covering exactly what the 11 MVP screens in `docs/analysis/mvp/screens-and-features.md` need, scoped by the cut-lines in `docs/analysis/mvp/scope-decisions.md` (EUR only, basic VAT, single project, single owner, private pilot). Full reconciliation and PDF-OCR import are in MVP scope; live bank feed integration is not. As with the other analysis docs, this is meant to be extended — later phases (multi-project, collaboration, bank feed integration, full tax compliance) get their own commands and queries added here when those phases are scoped, not invented now.

Each command/query below is fully specified — HTTP verb, route, request shape, response shape — so a coding agent can implement against it without guessing. Field shapes use a compact pseudo-JSON: `"fieldName": "type — note"`. Optional fields are marked `optional`; everything else is required.

## Conventions

- **Naming:** commands are imperative (`IssueInvoice`); queries are `Get...` (single item) or `List...` (collection), matching `architecture-guidelines.md`'s Domain Events section, which expects a command to map onto a past-tense domain event (`IssueInvoice` → `InvoiceIssued`).
- **Endpoints:** each service is its own deployable (per `services.md`), so each hosts its own `/api/v1/...` root — there's no shared gateway path prefix between them. The Experience API has a separate `/experience/v1/...` root of its own, screen-shaped rather than resource-shaped, and is the only thing the React frontend calls directly (per `architecture-guidelines.md`). Every route below is relative to its service's own `/api/v1` root unless stated otherwise.
- **HTTP verbs**, per `backend-api-code-guidelines.md`: `POST` for commands that create a resource or invoke a named business action (`.../{id}/issue`, `.../{id}/archive`, `.../{id}/reconcile`); `PATCH` for partial updates to an existing resource, with explicit patch semantics; nothing uses `PUT` in this catalog (no full-replacement command is defined for MVP).
- **IDs:** every entity ID is a GUID, serialized as a string.
- **Dates and times:** a business date (invoice date, due date, transaction date) is an ISO 8601 date (`"2026-09-06"`); a timestamp (`createdAt`, `updatedAt`) is an ISO 8601 date-time with UTC offset.
- **Money:** every money field is an integer in EUR minor units (cents) and is named with a `Minor` suffix (e.g. `netAmountMinor`), per `scope-decisions.md`'s EUR-only decision and `tech-stack.md`'s integer-minor-unit convention.
- **Idempotency:** every command requires an `Idempotency-Key` request header (a client-generated GUID); replaying the same key returns the original result rather than repeating the effect. Not repeated per command below.
- **Optimistic concurrency:** every command that updates an existing resource (`PATCH`, and action endpoints like `/archive` or `/issue`) requires an `If-Match: "<version>"` request header carrying the resource's current `version` (returned on every read and write of that resource); a stale version returns `409 Conflict`. Not repeated per command below.
- **Auth context:** every command and query is authorized from claims (`tenant_id`, `user_id`, `role`) on a short-lived internal JWT issued per-request by the Experience API — see `services.md`'s Authentication & Authorization section for the full propagation design. `tenant_id` scopes every request implicitly; it is never accepted as a request field.
- **Errors:** every endpoint uses RFC 7807 Problem Details (already wired via `AddProblemDetails()` in the scaffold) for `400` (structural validation), `404` (not found or not visible to this tenant), `409` (concurrency conflict or business-invariant violation), and `422` (a request-level business rule failure, e.g. a fabricated cross-service reference). Only errors beyond this default set are called out per command.
- **Lists:** every `List...` query returns a paginated envelope: `{ "items": [...], "page": "int", "pageSize": "int", "totalCount": "int" }`, accepting `page` (1-based, default 1), `pageSize` (default 25, max 100), `sort` (e.g. `"date:desc"`), `search` (free-text, where the query supports it), plus the resource-specific filters listed per query.

## Identity & Tenancy

Screens: none directly (Identity & Tenancy backs the app-wide auth-aware shell; its data surfaces through Settings and User).

### Commands

| Command | Route | Screen |
|---|---|---|
| `ProvisionTenant` | `POST /api/v1/tenants` | Manual onboarding (not a screen) |
| `Login` | `POST /api/v1/auth/login` | App-wide auth-aware shell (not a screen) |
| `UpdateUserProfile` | `PATCH /api/v1/users/me/profile` | User |
| `UpdateUserPreferences` | `PATCH /api/v1/users/me/preferences` | Settings |
| `ChangePassword` | `POST /api/v1/users/me/change-password` | Settings |

#### `ProvisionTenant`

Manual/admin-driven per `scope-decisions.md`'s private-pilot decision — not called from a screen. Creates Tenant + owner User + the one auto-provisioned Project transactionally.

**Route:** `POST /api/v1/tenants`

**Request:**
```
{
  "tenantName": "string",
  "ownerName": "string",
  "ownerEmail": "string",
  "initialPassword": "string — set directly since onboarding is admin-driven, not self-serve; ASP.NET Core Identity's own store holds the credential, not the User entity in domain.md"
}
```

**Response:** `201 Created`
```
{
  "tenantId": "guid",
  "ownerUserId": "guid",
  "projectId": "guid",
  "createdAt": "date-time"
}
```

**Errors:** `409 Conflict` if `ownerEmail` is already in use by another tenant.

**Events:** `identity.tenant-created.v1`, `identity.project-created.v1`.

#### `Login`

Validates credentials against the Identity credential store created in `ProvisionTenant`/Task 1.4, then signs the caller in via ASP.NET Core Identity's cookie authentication (per `services.md`'s Authentication & Authorization section). **Anonymous endpoint** — the only one on this service. Exposed directly on the Identity & Tenancy service for Phase 1 (built/tested in Task 1.5, same "call it directly before the Experience API exists" pattern as `ProvisionTenant` in Task 1.4). The browser-facing login the frontend actually calls is `POST /experience/v1/auth/login` (documented in the Experience API section below); see Open Item 11 for how the two relate once Task 1.6 builds that endpoint.

**Route:** `POST /api/v1/auth/login`

**Request:**
```
{ "email": "string", "password": "string" }
```

**Response:** `204 No Content` — the ASP.NET Core Identity session cookie is set on the response.

**Errors:** `401 Unauthorized` for an unknown email or an incorrect password (the two are not distinguished, to avoid leaking which emails are registered).

#### `UpdateUserProfile`

**Route:** `PATCH /api/v1/users/me/profile`

**Request:**
```
{ "name": "string" }
```

**Response:** `200 OK`
```
{ "userId": "guid", "name": "string", "updatedAt": "date-time", "version": "int" }
```

#### `UpdateUserPreferences`

**Route:** `PATCH /api/v1/users/me/preferences`

**Request:** (any subset of the three fields — a true partial update)
```
{
  "theme": "string optional — \"light\" | \"dark\"",
  "language": "string optional — e.g. \"es\", \"en\"",
  "notificationsEnabled": "bool optional"
}
```

**Response:** `200 OK`
```
{
  "userId": "guid",
  "theme": "string",
  "language": "string",
  "notificationsEnabled": "bool",
  "updatedAt": "date-time",
  "version": "int"
}
```

#### `ChangePassword`

Handled largely by ASP.NET Core Identity per `tech-stack.md`, not bespoke logic.

**Route:** `POST /api/v1/users/me/change-password`

**Request:**
```
{ "currentPassword": "string", "newPassword": "string" }
```

**Response:** `204 No Content`

**Errors:** `400 Bad Request` if `currentPassword` doesn't match.

### Queries

| Query | Route | Screen |
|---|---|---|
| `GetCurrentUser` | `GET /api/v1/users/me` | Every authenticated page load; User, Settings |

#### `GetCurrentUser`

**Route:** `GET /api/v1/users/me`

**Response:** `200 OK`
```
{
  "userId": "guid",
  "tenantId": "guid",
  "projectId": "guid",
  "name": "string",
  "email": "string",
  "theme": "string",
  "language": "string",
  "notificationsEnabled": "bool",
  "version": "int"
}
```

### Not in MVP

Invite flow, collaborator roles, Project CRUD/rename/archive/reactivate endpoints (the entity and the `identity.project-*.v1` events from `events.md` exist, but no MVP command triggers `ProjectRenamed`, `ProjectArchived`, or `ProjectReactivated` — they stay defined for the later multi-project phase), public self-serve signup.

## Financial Accounts & Ledger

Screens: Financial Overview (accounts/cards section), Transactions.

### Commands

| Command | Route | Screen |
|---|---|---|
| `LinkBankAccount` | `POST /api/v1/bank-accounts` | Financial Overview |
| `ArchiveBankAccount` | `POST /api/v1/bank-accounts/{bankAccountId}/archive` | Financial Overview |
| `AddPaymentCardLabel` | `POST /api/v1/payment-cards` | Financial Overview |
| `RemovePaymentCardLabel` | `DELETE /api/v1/payment-cards/{cardId}` | Financial Overview |
| `RecordTransaction` | `POST /api/v1/transactions` | Transactions |
| `ImportTransactionsFromFile` | `POST /api/v1/transactions/import` | Transactions, Financial Overview |
| `UpdateTransaction` | `PATCH /api/v1/transactions/{transactionId}` | Transactions |
| `ArchiveTransaction` | `POST /api/v1/transactions/{transactionId}/archive` | Transactions |

#### `LinkBankAccount`

Validates `projectId` against this service's local `project_replica` (per `services.md`'s Cross-Service Data Consistency Strategy) — not a synchronous call to Identity & Tenancy.

**Route:** `POST /api/v1/bank-accounts`

**Request:**
```
{ "projectId": "guid", "accountNumber": "string", "bankName": "string" }
```

**Response:** `201 Created`
```
{ "bankAccountId": "guid", "status": "\"active\"", "createdAt": "date-time", "version": "int" }
```

**Errors:** `422 Unprocessable Entity` if `projectId` isn't found (or is archived) in the local `project_replica`.

**Event:** `ledger.bank-account-linked.v1`.

#### `ArchiveBankAccount`

**Route:** `POST /api/v1/bank-accounts/{bankAccountId}/archive`

**Request:** *(no body)*

**Response:** `200 OK`
```
{ "bankAccountId": "guid", "status": "\"archived\"", "updatedAt": "date-time", "version": "int" }
```

#### `AddPaymentCardLabel`

Metadata only, per `domain.md`'s Decision note — never a real card number.

**Route:** `POST /api/v1/payment-cards`

**Request:**
```
{
  "projectId": "guid",
  "label": "string — e.g. \"Visa ending 1234\"",
  "cardholderName": "string",
  "expirationDate": "date optional — informational only"
}
```

**Response:** `201 Created`
```
{ "cardId": "guid", "label": "string", "createdAt": "date-time" }
```

#### `RemovePaymentCardLabel`

Cards aren't cross-service referenced, so this is a real (hard) delete, unlike the archive pattern used elsewhere.

**Route:** `DELETE /api/v1/payment-cards/{cardId}`

**Response:** `204 No Content`

#### `RecordTransaction`

**Route:** `POST /api/v1/transactions`

**Request:**
```
{
  "bankAccountId": "guid",
  "amountMinor": "int",
  "date": "date",
  "type": "string — \"income\" | \"expense\""
}
```

**Response:** `201 Created`
```
{ "transactionId": "guid", "status": "\"active\"", "createdAt": "date-time", "version": "int" }
```

**Event:** `ledger.transaction-recorded.v1`.

#### `ImportTransactionsFromFile`

The Transactions screen's and Financial Overview's CSV/Excel import wizard, with bank-statement column mapping.

**Route:** `POST /api/v1/transactions/import` (`multipart/form-data`)

**Request:** (multipart form fields, not JSON)
```
file: binary — the CSV/Excel file
bankAccountId: guid
columnMapping: {
  "dateColumn": "string — source column header",
  "amountColumn": "string",
  "typeColumn": "string optional — if omitted, sign of amount determines income/expense",
  "descriptionColumn": "string optional"
}
```

**Response:** `200 OK`
```
{
  "importedCount": "int",
  "transactionIds": ["guid"],
  "skippedRows": [ { "rowNumber": "int", "reason": "string" } ]
}
```

**Errors:** `422 Unprocessable Entity` with a `skippedRows`-shaped problem extension if the file can't be parsed at all (wrong format, no header row).

**Event:** one `ledger.transaction-recorded.v1` per imported row.

#### `UpdateTransaction`

**Route:** `PATCH /api/v1/transactions/{transactionId}`

**Request:** (any subset)
```
{
  "bankAccountId": "guid optional",
  "amountMinor": "int optional",
  "date": "date optional",
  "type": "string optional — \"income\" | \"expense\""
}
```

**Response:** `200 OK` — full transaction, same shape as `GetTransactionById`.

**Errors:** `409 Conflict` if the transaction's `status` is `archived`.

#### `ArchiveTransaction`

Soft-delete, not hard-delete, since Transaction becomes cross-service-referenceable starting Phase 3 (`domain.md`'s Decision note).

**Route:** `POST /api/v1/transactions/{transactionId}/archive`

**Response:** `200 OK`
```
{ "transactionId": "guid", "status": "\"archived\"", "updatedAt": "date-time", "version": "int" }
```

**Event:** `ledger.transaction-archived.v1`.

### Queries

| Query | Route | Screen |
|---|---|---|
| `ListBankAccounts` | `GET /api/v1/bank-accounts` | Financial Overview |
| `ListPaymentCards` | `GET /api/v1/payment-cards` | Financial Overview |
| `ListTransactions` | `GET /api/v1/transactions` | Transactions |
| `GetTransactionById` | `GET /api/v1/transactions/{transactionId}` | Transactions |
| `ListUnreconciledTransactions` | `GET /api/v1/transactions/unreconciled` | Expenses, Revenues, Payments (reconciliation picker) |

#### `ListBankAccounts`

**Route:** `GET /api/v1/bank-accounts?status=`

**Query params:** `status` optional (`active` \| `archived`, default `active`).

**Response:** `200 OK`
```
{
  "items": [
    { "bankAccountId": "guid", "accountNumber": "string", "bankName": "string", "status": "string", "balanceMinor": "int — sum of active transactions" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `ListPaymentCards`

**Route:** `GET /api/v1/payment-cards`

**Response:** `200 OK`
```
{
  "items": [
    { "cardId": "guid", "label": "string", "cardholderName": "string", "expirationDate": "date optional" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `ListTransactions`

**Route:** `GET /api/v1/transactions`

**Query params:** `bankAccountId` optional, `type` optional (`income` \| `expense`), `status` optional (`active` \| `archived`, default `active`), `dateFrom`/`dateTo` optional, plus the shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    { "transactionId": "guid", "bankAccountId": "guid", "amountMinor": "int", "date": "date", "type": "string", "status": "string" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `GetTransactionById`

**Route:** `GET /api/v1/transactions/{transactionId}`

**Response:** `200 OK`
```
{
  "transactionId": "guid",
  "bankAccountId": "guid",
  "amountMinor": "int",
  "date": "date",
  "type": "string",
  "status": "string",
  "createdAt": "date-time",
  "updatedAt": "date-time",
  "version": "int"
}
```

#### `ListUnreconciledTransactions`

**Clarified here:** Financial Accounts & Ledger has no visibility into which of its transactions another service has reconciled against — that fact lives in Billing & Invoicing's or Bookkeeping & Planning's own database, not here. So this query returns every **active** transaction (identical to `ListTransactions?status=active`); the actual "unreconciled" filtering — subtracting the transaction IDs the calling service already knows it has reconciled — happens one layer up, in the Experience API's composition for the Expenses/Revenues/Payments reconciliation picker (see the Experience API section below). This route exists as a named, documented alias rather than reusing `ListTransactions` directly, so the intent at the call site (populating a reconciliation picker) stays explicit.

**Route:** `GET /api/v1/transactions/unreconciled`

**Query params:** `bankAccountId` optional, plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** identical shape to `ListTransactions`.

### Not in MVP

Live bank feed integration (the Bank Feed Integration System API named in `api-led/system-apis.md`), real card tokenization or any PCI-scoped processing (cards are metadata-only per `scope-decisions.md`).

## Billing & Invoicing

Screens: Invoices, Payments.

**Resolved here:** the original note that "creation and issuing may be the same step" is settled — `CreateInvoice` creates a `draft`, `IssueInvoice` transitions it to `issued` and fires the event, and `VoidInvoice` only accepts a `draft`. This mirrors the `status` enum added to `domain.md`'s Invoice entity and matches the Invoices screen's need to show draft invoices before they're sent.

### Commands

| Command | Route | Screen |
|---|---|---|
| `CreateCounterparty` | `POST /api/v1/counterparties` | Invoices |
| `UpdateCounterparty` | `PATCH /api/v1/counterparties/{counterpartyId}` | Invoices |
| `ArchiveCounterparty` | `POST /api/v1/counterparties/{counterpartyId}/archive` | Invoices |
| `CreateInvoice` | `POST /api/v1/invoices` | Invoices |
| `IssueInvoice` | `POST /api/v1/invoices/{invoiceId}/issue` | Invoices |
| `VoidInvoice` | `POST /api/v1/invoices/{invoiceId}/void` | Invoices |
| `RecordPayment` | `POST /api/v1/payments` | Payments |
| `ReconcilePaymentWithTransaction` | `POST /api/v1/payments/{paymentId}/reconcile` | Payments |

#### `CreateCounterparty`

**Route:** `POST /api/v1/counterparties`

**Request:**
```
{
  "type": "string — \"customer\" | \"supplier\"",
  "name": "string",
  "taxId": "string optional",
  "email": "string optional",
  "address": "string optional"
}
```

**Response:** `201 Created`
```
{ "counterpartyId": "guid", "status": "\"active\"", "createdAt": "date-time", "version": "int" }
```

#### `UpdateCounterparty`

**Route:** `PATCH /api/v1/counterparties/{counterpartyId}`

**Request:** (any subset)
```
{ "name": "string optional", "taxId": "string optional", "email": "string optional", "address": "string optional" }
```

**Response:** `200 OK` — full counterparty, same shape as `GetCounterpartyById`.

#### `ArchiveCounterparty`

**Route:** `POST /api/v1/counterparties/{counterpartyId}/archive`

**Response:** `200 OK`
```
{ "counterpartyId": "guid", "status": "\"archived\"", "updatedAt": "date-time", "version": "int" }
```

#### `CreateInvoice`

Creates a `draft` invoice — no event yet (only `IssueInvoice` fires one).

**Route:** `POST /api/v1/invoices`

**Request:**
```
{
  "projectId": "guid",
  "counterpartyId": "guid",
  "direction": "string — \"incoming\" | \"outgoing\"",
  "netAmountMinor": "int",
  "taxRate": "decimal — e.g. 0.21 for 21%",
  "date": "date",
  "dueDate": "date"
}
```

**Response:** `201 Created`
```
{
  "invoiceId": "guid",
  "status": "\"draft\"",
  "netAmountMinor": "int",
  "taxAmountMinor": "int — computed: netAmountMinor * taxRate",
  "totalAmountMinor": "int — computed: netAmountMinor + taxAmountMinor",
  "createdAt": "date-time",
  "version": "int"
}
```

**Errors:** `422 Unprocessable Entity` if `projectId` isn't found in the local `project_replica`, or `counterpartyId` doesn't exist/is archived.

#### `IssueInvoice`

**Route:** `POST /api/v1/invoices/{invoiceId}/issue`

**Request:** *(no body)*

**Response:** `200 OK`
```
{ "invoiceId": "guid", "status": "\"issued\"", "issuedAt": "date-time", "version": "int" }
```

**Errors:** `409 Conflict` if the invoice isn't currently `draft`.

**Event:** `billing.invoice-issued.v1`.

#### `VoidInvoice`

Draft only.

**Route:** `POST /api/v1/invoices/{invoiceId}/void`

**Request:** *(no body)*

**Response:** `200 OK`
```
{ "invoiceId": "guid", "status": "\"void\"", "updatedAt": "date-time", "version": "int" }
```

**Errors:** `409 Conflict` if the invoice isn't currently `draft`.

#### `RecordPayment`

**Route:** `POST /api/v1/payments`

**Request:**
```
{
  "invoiceId": "guid",
  "amountMinor": "int",
  "date": "date",
  "paymentMethod": "string — e.g. \"bank_transfer\", \"card\""
}
```

**Response:** `201 Created`
```
{ "paymentId": "guid", "invoiceId": "guid", "amountMinor": "int", "date": "date", "paymentMethod": "string", "createdAt": "date-time", "version": "int" }
```

**Errors:** `409 Conflict` if the invoice is `draft` or `void` (a payment can only be recorded against an `issued`, `paid`, or `overdue` invoice).

**Events:** `billing.payment-recorded.v1`; also `billing.invoice-paid.v1` once this invoice's payments sum to its `totalAmountMinor`.

#### `ReconcilePaymentWithTransaction`

Validates `transactionId` against this service's local `transaction_replica` (per `services.md`'s Cross-Service Data Consistency Strategy).

**Route:** `POST /api/v1/payments/{paymentId}/reconcile`

**Request:**
```
{ "transactionId": "guid" }
```

**Response:** `200 OK`
```
{ "paymentId": "guid", "reconciledTransactionId": "guid", "updatedAt": "date-time", "version": "int" }
```

**Errors:** `422 Unprocessable Entity` if `transactionId` isn't found (or is archived) in the local `transaction_replica`; `409 Conflict` if this payment is already reconciled against a different transaction (one-to-one only, per `scope-decisions.md`).

### Queries

| Query | Route | Screen |
|---|---|---|
| `ListCounterparties` | `GET /api/v1/counterparties` | Invoices |
| `GetCounterpartyById` | `GET /api/v1/counterparties/{counterpartyId}` | Invoices |
| `ListInvoices` | `GET /api/v1/invoices` | Invoices |
| `GetInvoiceById` | `GET /api/v1/invoices/{invoiceId}` | Invoices |
| `GenerateInvoiceDocument` | `GET /api/v1/invoices/{invoiceId}/document` | Invoices |
| `ListPayments` | `GET /api/v1/payments` | Payments |

#### `ListCounterparties`

**Route:** `GET /api/v1/counterparties`

**Query params:** `type` optional (`customer` \| `supplier`), `status` optional (default `active`), plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    { "counterpartyId": "guid", "type": "string", "name": "string", "taxId": "string optional", "email": "string optional", "status": "string" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `GetCounterpartyById`

**Route:** `GET /api/v1/counterparties/{counterpartyId}`

**Response:** `200 OK`
```
{
  "counterpartyId": "guid", "type": "string", "name": "string", "taxId": "string optional",
  "email": "string optional", "address": "string optional", "status": "string",
  "createdAt": "date-time", "updatedAt": "date-time", "version": "int"
}
```

#### `ListInvoices`

**Route:** `GET /api/v1/invoices`

**Query params:** `status` optional (`draft` \| `issued` \| `paid` \| `overdue` \| `void`), `direction` optional (`incoming` \| `outgoing`), plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    {
      "invoiceId": "guid", "counterpartyId": "guid", "counterpartyName": "string",
      "direction": "string", "status": "string",
      "netAmountMinor": "int", "taxAmountMinor": "int", "totalAmountMinor": "int",
      "date": "date", "dueDate": "date"
    }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `GetInvoiceById`

**Route:** `GET /api/v1/invoices/{invoiceId}`

**Response:** `200 OK`
```
{
  "invoiceId": "guid", "projectId": "guid", "counterpartyId": "guid", "counterpartyName": "string",
  "direction": "string", "status": "string",
  "netAmountMinor": "int", "taxRate": "decimal", "taxAmountMinor": "int", "totalAmountMinor": "int",
  "date": "date", "dueDate": "date",
  "payments": [
    { "paymentId": "guid", "amountMinor": "int", "date": "date", "paymentMethod": "string", "reconciledTransactionId": "guid optional" }
  ],
  "createdAt": "date-time", "updatedAt": "date-time", "version": "int"
}
```

#### `GenerateInvoiceDocument`

Read-only — not a state change, per the original note.

**Route:** `GET /api/v1/invoices/{invoiceId}/document`

**Response:** `200 OK`, `Content-Type: application/pdf` — the rendered invoice PDF as the response body (not JSON).

#### `ListPayments`

**Route:** `GET /api/v1/payments`

**Query params:** `invoiceId` optional, plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    { "paymentId": "guid", "invoiceId": "guid", "amountMinor": "int", "date": "date", "paymentMethod": "string", "reconciledTransactionId": "guid optional" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

### Background process (not a user-invoked command)

`MarkInvoicesOverdue` — a scheduled job comparing `dueDate` to the current date for every `issued` invoice, setting `status = overdue` and raising `billing.invoice-overdue.v1`. Not exposed as an endpoint; runs on an internal schedule (e.g. a hosted background service), with a test-only trigger hook for Task 3.6's manual verification (see `mvp/tasks.md`).

### Not in MVP

Facturae/SII/Verifactu compliance (`scope-decisions.md`), PDF invoice OCR import (that's Bookkeeping & Planning's Document Extraction adapter, below, not this service).

## Bookkeeping & Planning

Screens: Expenses, Revenues, Plans.

### Commands

| Command | Route | Screen |
|---|---|---|
| `RecordExpense` | `POST /api/v1/expenses` | Expenses |
| `UpdateExpense` | `PATCH /api/v1/expenses/{expenseId}` | Expenses |
| `DeleteExpense` | `DELETE /api/v1/expenses/{expenseId}` | Expenses |
| `ReconcileExpenseWithTransaction` | `POST /api/v1/expenses/{expenseId}/reconcile` | Expenses |
| `ImportExpenseFromDocument` | `POST /api/v1/expenses/import-document` | Expenses |
| `ConfirmImportedExpense` | `POST /api/v1/expenses/{expenseId}/confirm` | Expenses |
| `RecordRevenue` | `POST /api/v1/revenues` | Revenues |
| `UpdateRevenue` | `PATCH /api/v1/revenues/{revenueId}` | Revenues |
| `DeleteRevenue` | `DELETE /api/v1/revenues/{revenueId}` | Revenues |
| `ReconcileRevenueWithTransaction` | `POST /api/v1/revenues/{revenueId}/reconcile` | Revenues |
| `ImportRevenueFromDocument` | `POST /api/v1/revenues/import-document` | Revenues |
| `ConfirmImportedRevenue` | `POST /api/v1/revenues/{revenueId}/confirm` | Revenues |
| `CreatePlan` | `POST /api/v1/plans` | Plans |
| `UpdatePlan` | `PATCH /api/v1/plans/{planId}` | Plans |
| `ArchivePlan` | `POST /api/v1/plans/{planId}/archive` | Plans |
| `AddPlannedExpenseLine` | `POST /api/v1/plans/{planId}/planned-expenses` | Plans |
| `AddPlannedRevenueLine` | `POST /api/v1/plans/{planId}/planned-revenues` | Plans |

Expense and Revenue are structurally identical (same fields, same commands, same rules), so each pair below is documented once with both routes named.

#### `RecordExpense` / `RecordRevenue`

Manual entry — `importSource` and `confirmedAt` are set by the server, never accepted from the client.

**Routes:** `POST /api/v1/expenses`, `POST /api/v1/revenues`

**Request:**
```
{
  "projectId": "guid",
  "amountMinor": "int",
  "date": "date",
  "category": "string",
  "recurring": "bool",
  "recurringInterval": "string optional — \"weekly\" | \"monthly\" | \"yearly\", required if recurring is true"
}
```

**Response:** `201 Created`
```
{ "expenseId": "guid" /* or revenueId */, "importSource": "\"manual\"", "confirmedAt": "date-time", "createdAt": "date-time", "version": "int" }
```

**Errors:** `422 Unprocessable Entity` if `projectId` isn't found in the local `project_replica`.

**Event:** `bookkeeping.expense-recorded.v1` (or `bookkeeping.revenue-recorded.v1`) — fires immediately here, since `confirmedAt` is set on creation for a manual record.

#### `UpdateExpense` / `UpdateRevenue`

**Routes:** `PATCH /api/v1/expenses/{expenseId}`, `PATCH /api/v1/revenues/{revenueId}`

**Request:** (any subset)
```
{
  "amountMinor": "int optional", "date": "date optional", "category": "string optional",
  "recurring": "bool optional", "recurringInterval": "string optional"
}
```

**Response:** `200 OK` — full record, same shape as the corresponding `GetById`-style fields in `ListExpenses`/`ListRevenues`.

#### `DeleteExpense` / `DeleteRevenue`

Hard delete — Expense/Revenue aren't referenced cross-service the way Project/Transaction are, so no soft-delete/tombstone is needed.

**Routes:** `DELETE /api/v1/expenses/{expenseId}`, `DELETE /api/v1/revenues/{revenueId}`

**Response:** `204 No Content`

#### `ReconcileExpenseWithTransaction` / `ReconcileRevenueWithTransaction`

Validates `transactionId` against this service's local `transaction_replica`.

**Routes:** `POST /api/v1/expenses/{expenseId}/reconcile`, `POST /api/v1/revenues/{revenueId}/reconcile`

**Request:**
```
{ "transactionId": "guid" }
```

**Response:** `200 OK`
```
{ "expenseId": "guid" /* or revenueId */, "reconciledTransactionId": "guid", "updatedAt": "date-time", "version": "int" }
```

**Errors:** `422 Unprocessable Entity` if `transactionId` isn't found/is archived in the local replica; `409 Conflict` if already reconciled against a different transaction.

#### `ImportExpenseFromDocument` / `ImportRevenueFromDocument`

Calls the Document Extraction adapter (`api-led/system-apis.md`) and creates a **draft** record — `confirmedAt` is left null.

**Routes:** `POST /api/v1/expenses/import-document`, `POST /api/v1/revenues/import-document` (`multipart/form-data`)

**Request:**
```
file: binary — the receipt/invoice PDF
projectId: guid
```

**Response:** `201 Created`
```
{
  "expenseId": "guid" /* or revenueId */,
  "importSource": "\"pdf_ocr\"",
  "confirmedAt": "null",
  "extracted": {
    "amountMinor": "int — OCR best guess",
    "date": "date optional — OCR best guess",
    "category": "string optional — OCR best guess",
    "confidence": "decimal optional — 0.0-1.0, if the adapter provides one"
  },
  "createdAt": "date-time",
  "version": "int"
}
```

**Errors:** `422 Unprocessable Entity` if the OCR adapter can't extract a usable amount at all (still worth creating an empty draft for manual fill-in — this is a UX call for the frontend to make, not a hard failure).

**Note:** no event fires here — only `ConfirmImportedExpense`/`ConfirmImportedRevenue` does.

#### `ConfirmImportedExpense` / `ConfirmImportedRevenue`

Sets `confirmedAt`, optionally correcting any of the OCR-extracted fields first.

**Routes:** `POST /api/v1/expenses/{expenseId}/confirm`, `POST /api/v1/revenues/{revenueId}/confirm`

**Request:** (any subset — corrections to what OCR extracted; omit a field to accept the extracted value as-is)
```
{
  "amountMinor": "int optional", "date": "date optional", "category": "string optional",
  "recurring": "bool optional", "recurringInterval": "string optional"
}
```

**Response:** `200 OK`
```
{ "expenseId": "guid" /* or revenueId */, "importSource": "\"pdf_ocr\"", "confirmedAt": "date-time", "updatedAt": "date-time", "version": "int" }
```

**Errors:** `409 Conflict` if `confirmedAt` is already set (already confirmed).

**Event:** `bookkeeping.expense-recorded.v1` (or `bookkeeping.revenue-recorded.v1`) — only raised here, not on import.

#### `CreatePlan`

**Route:** `POST /api/v1/plans`

**Request:**
```
{
  "projectId": "guid",
  "title": "string",
  "description": "string optional",
  "allocatedAmountMinor": "int optional",
  "startDate": "date",
  "endDate": "date"
}
```

**Response:** `201 Created`
```
{ "planId": "guid", "status": "\"active\"", "createdAt": "date-time", "version": "int" }
```

**Event:** `bookkeeping.plan-created.v1`.

#### `UpdatePlan`

**Route:** `PATCH /api/v1/plans/{planId}`

**Request:** (any subset)
```
{ "title": "string optional", "description": "string optional", "allocatedAmountMinor": "int optional", "startDate": "date optional", "endDate": "date optional" }
```

**Response:** `200 OK` — full plan, same shape as `GetPlanById`.

#### `ArchivePlan`

**Route:** `POST /api/v1/plans/{planId}/archive`

**Response:** `200 OK`
```
{ "planId": "guid", "status": "\"archived\"", "updatedAt": "date-time", "version": "int" }
```

#### `AddPlannedExpenseLine` / `AddPlannedRevenueLine`

**Routes:** `POST /api/v1/plans/{planId}/planned-expenses`, `POST /api/v1/plans/{planId}/planned-revenues`

**Request:**
```
{
  "amountMinor": "int",
  "date": "date",
  "category": "string",
  "recurring": "bool",
  "recurringInterval": "string optional"
}
```

**Response:** `201 Created`
```
{ "plannedExpenseId": "guid" /* or plannedRevenueId */, "planId": "guid", "createdAt": "date-time" }
```

**Event:** `bookkeeping.planned-expense-added.v1` (or `bookkeeping.planned-revenue-added.v1`).

### Queries

| Query | Route | Screen |
|---|---|---|
| `ListExpenses` | `GET /api/v1/expenses` | Expenses |
| `ListRevenues` | `GET /api/v1/revenues` | Revenues |
| `ListPlans` | `GET /api/v1/plans` | Plans |
| `GetPlanById` | `GET /api/v1/plans/{planId}` | Plans |
| `GetPlanVsActual` | `GET /api/v1/plans/{planId}/plan-vs-actual` | Plans |

#### `ListExpenses` / `ListRevenues`

By default, excludes unconfirmed drafts (`confirmedAt IS NULL`) — pass `includeDrafts=true` to see them (needed by the Expenses/Revenues screen's own draft-review step).

**Routes:** `GET /api/v1/expenses`, `GET /api/v1/revenues`

**Query params:** `category` optional, `includeDrafts` optional (bool, default `false`), `dateFrom`/`dateTo` optional, plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    {
      "expenseId": "guid" /* or revenueId */, "amountMinor": "int", "date": "date", "category": "string",
      "recurring": "bool", "recurringInterval": "string optional",
      "importSource": "string", "confirmedAt": "date-time optional",
      "reconciledTransactionId": "guid optional"
    }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `ListPlans`

**Route:** `GET /api/v1/plans`

**Query params:** `status` optional (default `active`), plus shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [
    { "planId": "guid", "title": "string", "allocatedAmountMinor": "int optional", "startDate": "date", "endDate": "date", "status": "string" }
  ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `GetPlanById`

**Route:** `GET /api/v1/plans/{planId}`

**Response:** `200 OK`
```
{
  "planId": "guid", "title": "string", "description": "string optional",
  "allocatedAmountMinor": "int optional", "startDate": "date", "endDate": "date", "status": "string",
  "plannedExpenses": [ { "plannedExpenseId": "guid", "amountMinor": "int", "date": "date", "category": "string", "recurring": "bool" } ],
  "plannedRevenues": [ { "plannedRevenueId": "guid", "amountMinor": "int", "date": "date", "category": "string", "recurring": "bool" } ],
  "createdAt": "date-time", "updatedAt": "date-time", "version": "int"
}
```

#### `GetPlanVsActual`

Joins this service's own actuals (Expense/Revenue, confirmed only) against the plan's planned lines — no cross-service call needed, since Expense/Revenue and Plan are all owned here.

**Route:** `GET /api/v1/plans/{planId}/plan-vs-actual`

**Response:** `200 OK`
```
{
  "planId": "guid",
  "period": { "startDate": "date", "endDate": "date" },
  "expenses": { "plannedMinor": "int", "actualMinor": "int", "varianceMinor": "int" },
  "revenues": { "plannedMinor": "int", "actualMinor": "int", "varianceMinor": "int" },
  "byCategory": [
    { "category": "string", "plannedMinor": "int", "actualMinor": "int" }
  ]
}
```

### Not in MVP

Approval workflows or multi-scenario forecasting on Plans. Splitting one Transaction's reconciliation across multiple Expenses/Revenues, or one Expense/Revenue across multiple Transactions (reconciliation is one-to-one for MVP — see `events.md` Open Items).

## Reporting

Screens: Home (real widgets), Financial Overview, Reports.

Unlike the other four services, Reporting's queries read from projections built off the other services' integration events (per `services.md`), not from a locally-owned transactional aggregate. Its MVP write surface is small but not empty — the Reports screen lets a user generate, save, edit, and delete reports, so `Report` needs a thin write surface for its own metadata (title, criteria), separate from the financial content, which is always computed fresh from projections rather than frozen at save time (per `domain.md`'s Report decision).

### Commands

| Command | Route | Screen |
|---|---|---|
| `SaveReport` | `POST /api/v1/reports` | Reports |
| `UpdateReport` | `PATCH /api/v1/reports/{reportId}` | Reports |
| `DeleteReport` | `DELETE /api/v1/reports/{reportId}` | Reports |

#### `SaveReport`

**Route:** `POST /api/v1/reports`

**Request:**
```
{
  "title": "string",
  "criteria": {
    "dateFrom": "date",
    "dateTo": "date",
    "categories": ["string"] // optional
  }
}
```

**Response:** `201 Created`
```
{ "reportId": "guid", "title": "string", "createdAt": "date-time", "version": "int" }
```

#### `UpdateReport`

**Route:** `PATCH /api/v1/reports/{reportId}`

**Request:** (any subset)
```
{ "title": "string optional", "criteria": "object optional — same shape as SaveReport" }
```

**Response:** `200 OK` — full report, same shape as `GetReportById`'s metadata fields.

#### `DeleteReport`

**Route:** `DELETE /api/v1/reports/{reportId}`

**Response:** `204 No Content`

### Queries

| Query | Route | Screen |
|---|---|---|
| `GetFinancialOverview` | `GET /api/v1/financial-overview` | Home, Financial Overview |
| `ListReports` | `GET /api/v1/reports` | Reports |
| `GetReportById` | `GET /api/v1/reports/{reportId}` | Reports |

#### `GetFinancialOverview`

**Route:** `GET /api/v1/financial-overview`

**Query params:** `dateFrom`/`dateTo` optional (default: current month).

**Response:** `200 OK`
```
{
  "period": { "dateFrom": "date", "dateTo": "date" },
  "totals": {
    "revenueMinor": "int", "expenseMinor": "int", "netMinor": "int",
    "invoicedMinor": "int", "unpaidInvoicedMinor": "int", "overdueInvoicedMinor": "int"
  },
  "trend": [
    { "period": "string — e.g. \"2026-08\"", "revenueMinor": "int", "expenseMinor": "int" }
  ],
  "planStatus": [
    { "planId": "guid", "title": "string", "allocatedAmountMinor": "int optional", "actualMinor": "int" }
  ]
}
```

#### `ListReports`

**Route:** `GET /api/v1/reports`

**Query params:** shared `search`/`sort`/`page`/`pageSize`.

**Response:** `200 OK`
```
{
  "items": [ { "reportId": "guid", "title": "string", "criteria": "object", "updatedAt": "date-time" } ],
  "page": "int", "pageSize": "int", "totalCount": "int"
}
```

#### `GetReportById`

Content is always computed fresh at read time from the criteria stored with the report — never a frozen snapshot.

**Route:** `GET /api/v1/reports/{reportId}`

**Response:** `200 OK`
```
{
  "reportId": "guid", "title": "string", "criteria": "object — same shape as SaveReport's criteria",
  "content": {
    "totals": { "revenueMinor": "int", "expenseMinor": "int", "netMinor": "int" },
    "byCategory": [ { "category": "string", "amountMinor": "int" } ]
  },
  "createdAt": "date-time", "updatedAt": "date-time", "version": "int"
}
```

## Experience API

No commands or queries of its own — every endpoint here composes calls to the five services above (each with the internal JWT described in `services.md`'s Authentication & Authorization section) and shapes the result for one screen. This is the only layer the React frontend calls, per `architecture-guidelines.md`.

**Auth endpoints** (not screen-specific):

| Endpoint | Route | Composes |
|---|---|---|
| Login | `POST /experience/v1/auth/login` | Validates credentials through Identity & Tenancy's internal `POST /api/v1/auth/validate-credentials` endpoint, then sets the Experience API's session cookie. |
| Logout | `POST /experience/v1/auth/logout` | Clears the Experience API session cookie. |

**Screen endpoints** — one read endpoint per MVP screen, plus write endpoints that mostly forward 1:1 to the owning service's command (same request/response shape as documented above unless noted as composed):

| Screen | Read route | Composes (reads) | Notable composed writes |
|---|---|---|---|
| Home | `GET /experience/v1/home` | Reporting's `GetFinancialOverview` (compact form) + Identity & Tenancy's `GetCurrentUser` (for the greeting) | — |
| Financial Overview | `GET /experience/v1/financial-overview` | Reporting's `GetFinancialOverview` + Ledger's `ListBankAccounts`/`ListPaymentCards` | `POST /experience/v1/financial-overview/transactions/import` → Ledger's `ImportTransactionsFromFile` |
| Expenses | `GET /experience/v1/expenses` | Bookkeeping's `ListExpenses` | `GET /experience/v1/expenses/unreconciled-transactions` → composes Ledger's `ListUnreconciledTransactions` minus this tenant's already-reconciled `reconciled_transaction_id`s (fetched from Bookkeeping) — the cross-service diff described in the Financial Accounts & Ledger section above |
| Revenues | `GET /experience/v1/revenues` | Bookkeeping's `ListRevenues` | Same reconciliation-picker composition as Expenses |
| Invoices | `GET /experience/v1/invoices` | Billing's `ListInvoices` + `ListCounterparties` | — |
| Payments | `GET /experience/v1/payments` | Billing's `ListPayments` | `GET /experience/v1/payments/unreconciled-transactions` → same composition pattern as Expenses/Revenues |
| Transactions | `GET /experience/v1/transactions` | Ledger's `ListTransactions` | — |
| Reports | `GET /experience/v1/reports` | Reporting's `ListReports`/`GetReportById` | — |
| Plans | `GET /experience/v1/plans` | Bookkeeping's `ListPlans`/`GetPlanById`/`GetPlanVsActual` | — |
| Settings | `GET /experience/v1/settings` | Identity & Tenancy's `GetCurrentUser` (preferences fields) | `PATCH /experience/v1/settings` → `UpdateUserPreferences`; `POST /experience/v1/settings/change-password` → `ChangePassword` |
| User | `GET /experience/v1/user` | Identity & Tenancy's `GetCurrentUser` (profile fields) | `PATCH /experience/v1/user` → `UpdateUserProfile` |

Every other write action named in a service section above (creating an invoice, recording an expense, linking a bank account, and so on) is exposed at the Experience API under the matching screen's route prefix (e.g. `POST /experience/v1/invoices` forwards to Billing's `CreateInvoice` with the same request/response shape) — these aren't re-listed individually since they're pure pass-throughs, not compositions.

## Open Items

**Resolved 2026-09-06 (this pass):**

1. ~~Every command/query lacked a concrete HTTP verb, route, request shape, and response shape~~ — resolved: fully specified above, service by service.
2. ~~`CreateInvoice`/`IssueInvoice` — "may be the same step" was left unresolved~~ — resolved: they're two explicit steps (`draft` → `issued`), matching the `status` enum now on `domain.md`'s Invoice entity.
3. ~~`ListUnreconciledTransactions` implied Financial Accounts & Ledger could see foreign-service reconciliation, which it can't~~ — resolved: it returns all active transactions; the actual cross-service "unreconciled" diff happens at the Experience API composition layer for the Expenses/Revenues/Payments reconciliation picker.
4. ~~Invoice, Counterparty, Bank Account, and Plan had archive/status-transition commands but no status field in `domain.md`~~ — resolved: `domain.md` updated with a `status` field on each (Invoice gets a full `draft`/`issued`/`paid`/`overdue`/`void` enum; the other three get the existing `active`/`archived` pattern).
5. ~~User preferences (theme, language, notifications) had no home in `domain.md`~~ — resolved: added directly to the User entity.
6. ~~Report's `content` field contradicted the "always computed fresh, never frozen" decision~~ — resolved: `domain.md`'s Report entity now stores `criteria`, not `content`.
7. ~~Expense/Revenue's OCR "draft until confirmed" rule had no field to enforce it~~ — resolved: added `confirmed_at` to both in `domain.md`.

**Still open:**

8. **`ListUnreconciledTransactions`'s Experience-API-level composition** (item 3 above) is specified at the shape level here, but the exact mechanism for the Experience API to know a service's already-reconciled transaction IDs (a dedicated lightweight query on Billing/Bookkeeping, vs. deriving it from `ListPayments`/`ListExpenses`/`ListRevenues`'s existing `reconciledTransactionId` field) isn't chosen — the latter is simplest and needs no new endpoint, but flagging this as a decision point for whoever implements Task 2.6/3.7/4.6's composition logic.
9. `GetFinancialOverview`'s and `GetPlanVsActual`'s exact trend-bucketing (monthly? weekly?) and category-grouping rules aren't pinned down — reasonable defaults are shown above (monthly trend, flat category list) but should be confirmed against the actual Financial Overview/Plans screen designs once those exist, per Task 5.1/4.1's spec-check step.
10. The `skippedRows` shape on `ImportTransactionsFromFile`'s error response, and the equivalent for a malformed CSV, are illustrative — the exact validation-error vocabulary should be finalized during Task 2.5.
11. ~~How Identity & Tenancy's own `POST /api/v1/auth/login` relates to the Experience API's `POST /experience/v1/auth/login`~~ — resolved in Task 1.6: the Experience API owns the browser session cookie. It validates credentials through Identity & Tenancy's internal `POST /api/v1/auth/validate-credentials` endpoint, which returns only the authenticated tenant/user/role claims; the browser never receives or forwards Identity's cookie.
