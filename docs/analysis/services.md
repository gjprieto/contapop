# Services

This document defines the deployable service boundaries for Contapop, following the API-led connectivity model in `docs/standards/architecture-guidelines.md` and the System API analysis in `docs/analysis/api-led/system-apis.md`. It groups the System APIs derived from `docs/analysis/domain.md` into seven services rather than deploying one service per System API — each service is a genuine bond of shared data ownership and lifecycle, not just a technical convenience. MVP-specific scope cuts (currency, invoice tax handling, single-project, single-user) are tracked separately in `docs/analysis/mvp/scope-decisions.md` and referenced below where they reduce a service's near-term build.

Within every service, each System API it hosts is still an independently documented HTTP surface (own route prefix, own OpenAPI contract, own database schema even where the schema lives in the same PostgreSQL instance). No code reaches across schemas. This keeps a later split — pulling one System API out into its own service — a matter of moving a schema and a folder, not a redesign.

## Identity & Tenancy Service

**Hosts:** Identity & Tenancy System API
**Owns:** Tenant, User, Project

The foundational service. Tenant, User, and Project share a lifecycle (created once, rarely change, referenced by ID everywhere else) and the same concerns: authentication, roles/permissions, multi-tenant isolation, and user preferences (theme, language, notifications). Every other service depends on it; it depends on nothing else. See the **Authentication & Authorization** section below for how this service's identity data backs authentication across every other service.

**MVP scope note (see `docs/analysis/mvp/scope-decisions.md`):** the MVP build here is smaller than the full design — single-owner only (no invite flow, no collaborator role) and one Project auto-provisioned per Tenant (no Project management UI or CRUD surface). Neither of these changes the entity shapes or the sync events already documented; both are fast-follow additions, not rebuilds.

**Split trigger:** none anticipated — this stays a stable platform service.

## Financial Accounts & Ledger Service

**Hosts:** Bank Accounts, Payment Cards, and Transactions System APIs
**Owns:** Bank Account, Credit or Debit Card, Transaction, Transaction Reconciliation Claim

Manages the tenant's linked financial sources and the movement ledger fed by them. Bank Accounts and Payment Cards share a data owner and evolve together as real bank/card integrations mature; Transactions is the append-heavy stream those sources feed, whether from manual CSV/Excel import today or a live bank feed later. **Decision (2026-09-06): Payment Cards are metadata-only for the MVP** — a user-entered label only, no real card number stored — so there is no PCI scope to isolate for right now; this is revisited if real card processing is ever added. **Decision (2026-09-06): Transaction is now also a reference-data hub** — since Expense, Revenue, and Payment can be reconciled against it (see Cross-Service Data Consistency Strategy below), it publishes its own lifecycle events alongside Identity & Tenancy's Project events.

**Split trigger:** bank-feed ingestion needs its own scaling/reliability profile, or card handling moves to a real tokenization vendor with genuine PCI scope.

## Billing & Invoicing Service

**Hosts:** Invoicing and Payments System APIs, plus a Counterparties (Customer/Supplier) System API
**Owns:** Invoice or Ticket, Invoice Attachment, Payment, Counterparty (Customer/Supplier)

Invoice status derives from payment activity, and both need a bill-to/bill-from party. **Decision (2026-09-06): Counterparty is added to `domain.md`** as a first-class entity, referenced by Invoice via `counterparty_id` plus a `direction` field for the incoming/outgoing distinction the Invoices screen needs. This also fills the "Clients System API" already named as an example in `architecture-guidelines.md` with no backing entity. Keeping Invoicing, Payments, and Counterparties together avoids a synchronous cross-service round trip every time an invoice is closed. **Decision (2026-09-06): full reconciliation is in MVP scope** — Payment can be matched against a Financial Accounts & Ledger Transaction, a new cross-service reference this service must validate (see the Data Consistency Strategy below). **Decision (2026-09-12): invoice attachments belong to Billing** — Billing stores one attachment's metadata per invoice in PostgreSQL and its bytes in a private Azure Blob Storage container, using Azurite through the Aspire integration locally. Blob names are tenant-scoped and downloads are authorized through Billing; the frontend never receives storage credentials or a durable public blob URL. **Decision (2026-09-12): draft invoice removal is Billing-local** — Billing permanently deletes only a payment-free draft and its lines. It removes attachment metadata and records blob cleanup durably in the same database transaction, then retries physical blob deletion until it succeeds. No integration event is emitted because a draft was never sent to Reporting; invoices that entered the financial workflow retain the archival path and its event. **Decision (2026-09-12): Billing owns draft invoice edits** — `UpdateDraftInvoice` atomically replaces a draft's complete line collection and recomputes its VAT and aggregate amounts. Counterparty, direction, and type remain creation-time identity fields; edits do not publish an event because Reporting receives Invoice facts only after issue.

**Decision (2026-09-12, supersedes the prior single-attachment wording above):** Billing stores a collection of invoice-attachment metadata in PostgreSQL and their bytes in a private Azure Blob Storage container, using Azurite through the Aspire integration locally. An Invoice has at most one `invoice` attachment and any number of `other` attachments; the type invariant is enforced transactionally. Blob names are tenant-scoped and downloads are authorized through Billing; the frontend never receives storage credentials or a durable public blob URL. Billing does not generate invoice PDFs. Archive and draft-deletion operations remove every attachment through durable cleanup records, while an individual replacement or removal affects only the explicitly addressed attachment.

**Split trigger:** Counterparties becomes genuine shared reference data needed independently by other bounded contexts.

## Bookkeeping & Planning Service

**Hosts:** Expenses, Revenues, and Planning System APIs
**Owns:** Expense, Revenue, Plan, Planned Revenue, Planned Expense

Actuals (Expenses, Revenues) and forecasts (Planning) are one comparison problem, consumed together by the Financial Overview screen and owned by the same team. **Decision (2026-09-06): `Budget` is merged into `Plan`** — the MVP screens only expose a "Plans" screen, and `Plan` now carries an optional `allocated_amount`/`start_date`/`end_date` absorbed from the former Budget entity. **Decision (2026-09-06): full reconciliation and PDF-OCR import are both in MVP scope** — Expense and Revenue can each be matched against a Financial Accounts & Ledger Transaction (a new cross-service reference this service must validate, per the Data Consistency Strategy below) and can be created from a PDF via OCR extraction, pending user confirmation before the record counts toward reports (see `docs/analysis/contracts.md`).

**Split trigger:** Planning grows real workflow (approvals, multi-scenario forecasting) independent of actuals.

## Reporting Service

**Hosts:** no System API — Process/read-model layer only
**Owns:** query projections built from the other four services' domain events; optionally, saved report metadata (the `Report` entity) if report definitions/exports need persistence

Never owns write-side data. Matches the `Contapop.Reporting.Service` structure already sketched in `architecture-guidelines.md` (Application/Infrastructure/Api layers only, no Domain layer). Consumes integration events published by the other services rather than querying their databases directly.

**Split trigger:** none — this is a consumer by design, not a candidate for further splitting.

## Experience API

**Hosts:** no System API — Experience layer
**Owns:** nothing; aggregates and shapes data from Billing & Invoicing, Bookkeeping & Planning, Financial Accounts & Ledger, Reporting, and Identity & Tenancy for the React web app

Thin, channel-specific boundary per `architecture-guidelines.md`. The frontend calls only this layer, never a System API directly.

**Split trigger:** a second client channel (e.g. mobile) needs its own Experience API.

## Reconciliation Process API

**Hosts:** Transaction Reconciliation coordinator API
**Owns:** durable reconciliation-operation retry records only

This internal Process API coordinates the narrow strict-global-reconciliation protocol. It persists an operation before reserving a Ledger claim, invokes Billing or Bookkeeping's idempotent dependent reconciliation, records the outcome, and confirms or releases the claim. It retries failed confirmation and release operations. The Experience API remains stateless and calls this Process API rather than retaining durable orchestration state.

**Split trigger:** none anticipated. This is a narrowly scoped Process API, not a general saga framework.

## Summary

| Service | System APIs hosted | Entities owned |
|---|---|---|
| Identity & Tenancy | Identity & Tenancy | Tenant, User, Project |
| Financial Accounts & Ledger | Bank Accounts, Payment Cards, Transactions | Bank Account, Credit/Debit Card, Transaction |
| Billing & Invoicing | Invoicing, Payments, Counterparties | Invoice/Ticket, Invoice Attachment, Payment, Counterparty |
| Bookkeeping & Planning | Expenses, Revenues, Planning | Expense, Revenue, Plan, Planned Revenue, Planned Expense |
| Reporting | none (Process/read-model) | projections; optionally Report metadata |
| Experience API | none (Experience layer) | none — aggregates the above |
| Reconciliation Process API | Transaction Reconciliation coordinator | durable reconciliation-operation retry records only |

Seven deployables total, each with its own pipeline, Dapr sidecar, database, and on-call surface — versus eleven if every System API were deployed separately. This refines rather than replaces the starting service list in `architecture-guidelines.md`'s Module Structure section (`Identity, Clients, Invoicing, Expenses, Payments, Reporting`): it names Financial Accounts & Ledger and Bookkeeping & Planning explicitly, folds the placeholder "Clients" service into Billing & Invoicing as Counterparties, and adds the narrowly scoped reconciliation coordinator required for durable global uniqueness.

## Domain Questions Resolved (2026-09-06)

The four open questions carried over from the domain analysis are now settled, and `domain.md` has been updated accordingly:

1. **Budget vs. Plan** — merged. `Budget` is removed as a separate entity; `Plan` absorbs its `allocated_amount`/`start_date`/`end_date` shape.
2. **Missing Counterparty entity** — added. `Counterparty` (customer/supplier) is now a first-class entity owned by Billing & Invoicing, referenced from Invoice via `counterparty_id` and `direction`.
3. **Project's real meaning** — confirmed as an internal organizational grouping (e.g. personal vs. business books, or multiple business lines under one tenant), not a customer. It's a distinct concept from Counterparty, not the same one.
4. **Payment Cards MVP scope** — in scope, metadata-only: a user-entered label (e.g. "Visa ending 1234"), never a real card number. No tokenization vendor or PCI scope is needed for the MVP as a result.

## Data Storage

Yes — one PostgreSQL database per stateful service, consistent with the "independently owned database per service" rule already stated in `architecture-guidelines.md`. Six of the seven services need one; the Experience API doesn't.

| Service | Own database? | Internal schemas (one per hosted System API) |
|---|---|---|
| Identity & Tenancy | Yes — `contapop_identity` | tenancy, users |
| Financial Accounts & Ledger | Yes — `contapop_ledger` | bank_accounts, payment_cards, transactions |
| Billing & Invoicing | Yes — `contapop_billing` | invoicing, payments, counterparties |
| Bookkeeping & Planning | Yes — `contapop_bookkeeping` | expenses, revenues, planning |
| Reporting | Yes — `contapop_reporting` | read-model projections only, populated from the other services' integration events — never queries their databases directly |
| Experience API | No | stateless aggregator; at most a namespaced slice of the shared Redis cache for response caching, never authoritative data |
| Reconciliation Process API | Yes — `contapop_reconciliation` | reconciliation-operation records and retry state only |

Each database gets its own EF Core migration history and its own dedicated database role/credentials, so no service can query another's tables even accidentally — that's what "independently owned" is actually enforcing, not physical hardware separation. The outbox and inbox tables (per `architecture-guidelines.md`) live inside each service's own database, in the schema of the aggregate that emits or consumes the event — never a shared table.

**Physical layout for now:** run all six as separate databases on the single Aspire-managed PostgreSQL server already in `tech-stack.md`, using Aspire's `AddDatabase(...)` per stateful service. That gives full logical isolation (separate credentials, separate migrations, no cross-database joins possible) while keeping one server instance to operate, patch, and back up at this team size. Promoting any one of them to its own dedicated server instance later is purely an infrastructure change — a new connection string — since the application layer never assumed shared access in the first place.

**First candidate to split onto its own server instance:** Financial Accounts & Ledger, once real bank-feed ingestion volume needs its own scaling/reliability profile, or card handling reaches a compliance scope that calls for network-level isolation.

**Redis** stays a single shared instance across all services (as already provisioned), since its only uses per `architecture-guidelines.md` — output caching, distributed locks, outbox/inbox pub-sub — are explicitly non-authoritative. Namespace keys per service to avoid collisions; do not use it as the only copy of any financial data.

**Azure Blob Storage** stores invoice attachment bytes in a private Billing-owned container. Local development uses Aspire's Azure Storage integration with the Azurite emulator; production uses an Azure Storage account. Billing PostgreSQL remains authoritative for attachment ownership and metadata. All upload, download, replace, and removal operations go through authenticated Billing endpoints that enforce `tenant_id`; clients never access blobs through public URLs.

## Cross-Service Data Consistency Strategy

**Decision:** for any reference that crosses a service boundary, the referencing service validates against a locally replicated read cache of the referenced entity, kept current via event-driven propagation. A compensation mechanism for handling inconsistencies that slip through is deferred — named here as a backlog item, not designed yet.

### The mechanism

1. The owning service publishes a versioned integration event through its transactional outbox on every relevant lifecycle change of the referenced entity (create, update, archive/soft-delete) — per the Domain Events and Outbox sections of `architecture-guidelines.md`.
2. Each consuming service subscribes via its own inbox (idempotent, deduplicated by message ID, tolerant of out-of-order delivery by only applying an event if its aggregate version is newer than what the replica already has) and projects the event into a small local read-model table — only the fields that service actually needs, never the full aggregate.
3. At write time, the consuming service validates the reference against this local table. No synchronous cross-service call is made in the write path, except for the narrowly defined Transaction Reconciliation Claim protocol below.

### Where this applies across the six services

There are now two reference-data hubs (Tenant is resolved from the authenticated request's tenant claim instead of replication — see `events.md` for the full reasoning):

- **Identity & Tenancy** publishes Project lifecycle events. **Financial Accounts & Ledger** and **Billing & Invoicing** replicate Project to validate Bank Account/Card and Invoice creation respectively; **Bookkeeping & Planning** replicates it to validate Expense, Revenue, and Plan creation.
- **Financial Accounts & Ledger** publishes Transaction lifecycle events (added 2026-09-06, once reconciliation became MVP scope). **Bookkeeping & Planning** and **Billing & Invoicing** each replicate Transaction to validate the new `reconciled_transaction_id` on Expense/Revenue and Payment respectively.
- **Reporting** already replicates from every service by design — this is its core mechanism, not an addition.
- **Experience API** does not replicate anything; it composes at read time from the other services (see Data Storage section above), so this strategy doesn't apply to it.

References that stay inside one service (Invoice → Counterparty, Payment → Invoice, Transaction → Bank Account, Planned Revenue/Expense → Plan) are ordinary in-database foreign keys and aren't affected by this strategy.

### What "strict" means here, and what it doesn't

This gives every service a uniform, always-applied validation rule — a reference is never silently accepted without a check. What it does not give is linearizable freshness: there's an unavoidable propagation window between an event being emitted and a replica reflecting it, typically small under normal event-dispatch latency but non-zero. A Project created and immediately referenced from another service in the same user session is the scenario most likely to hit that window. Whether that window needs to be actively closed (e.g. by having the Experience API wait for propagation to be confirmed before allowing the dependent action) or just accepted is an open question for later.

### Strict global Transaction reconciliation

**Decision (2026-09-08):** an active Ledger Transaction can be reconciled with at most one Payment, Expense, or Revenue across all services. A local `transaction_replica` alone cannot enforce that invariant because Billing & Invoicing and Bookkeeping & Planning each see only their own records. Financial Accounts & Ledger therefore owns the `Transaction Reconciliation Claim` aggregate and is the single serialized authority for it.

This is a deliberate, narrow exception to the otherwise asynchronous reference-validation strategy. It is not a cross-service database dependency: the Experience API coordinates authenticated System API calls, and no service reads or writes another service's database.

1. The Experience API first durably records a reconciliation operation, then asks Ledger to reserve a Transaction for the target dependent type and ID. Ledger accepts only an active Transaction with no existing non-released claim, then durably creates a `reserved` claim with a short expiry.
2. The Experience API calls Billing or Bookkeeping to persist the dependent record's reconciliation. That service still validates the Transaction against its local active replica and validates the Ledger-issued claim for the same tenant, Transaction, dependent type, and dependent ID through the protocol.
3. After the dependent write succeeds, the Experience API records that success durably and confirms the claim with Ledger. A confirmed claim prevents all later reservations for that Transaction. If confirmation fails, the durable operation retries confirmation, never release.
4. If the dependent write fails, the Experience API records that failure and immediately releases the reservation. If that release call fails, the durable operation retries it. A reservation past `expires_at` is a recovery signal, not permission for Ledger to release it automatically: the coordinator first resolves the idempotent dependent operation. If it succeeded, it confirms; if it did not, it releases. An uncertain outcome therefore temporarily blocks the Transaction rather than risking a duplicate reconciliation.
5. When a reconciled Expense or Revenue is deleted, its service deletes the dependent record first and then releases its confirmed claim. If release fails, the durable operation retries it; the temporary stale claim preserves the uniqueness invariant. Payments have no delete or unreconcile command in MVP, so their confirmed claims remain. A future command that changes a reconciliation must reserve the replacement first, persist the change, confirm the new claim, and then release the old claim; it must use the same durable compensation protocol on failure.

Claims are not published as a general synchronization event and are not copied into local replicas: they are short, invariant-enforcement state owned only by Ledger. Archiving a Transaction retains any existing confirmed claim so the historical reconciled reference does not dangle; no new claim can be reserved for an archived Transaction.

### Compensation mechanism — deferred

Two situations this strategy doesn't resolve on its own, left as a named backlog item:

- A write is accepted against a reference that was valid in the local replica but has since been invalidated at the source (the owning entity was archived/deleted after the replica was read but before or shortly after the dependent write landed).
- A replica has drifted from the source (missed event, bug, replay gap) and something references a value that never should have validated.

Per `architecture-guidelines.md`'s Orchestrator Pattern, the intended shape of the remaining general fix is a Process-API-level saga (Dapr Workflow) triggered by a detected inconsistency — from the reconciliation/audit sweep discussed previously, or from a "reference no longer valid" event from the owning service — which then decides how to react (flag the dependent record for review, notify, or auto-remediate). The Transaction Reconciliation Claim protocol above is the one explicit MVP exception: it needs durable compensation now to enforce a global uniqueness invariant, but does not introduce a general-purpose saga for replica drift.

## Authentication & Authorization

**Decision (2026-09-06):** the browser-facing session and the internal service-to-service trust boundary use two different mechanisms, deliberately kept separate so splitting a service later never requires touching how users log in.

### Browser-facing authentication

- ASP.NET Core Identity with cookie authentication, terminated at the Experience API (per `docs/standards/tech-stack.md`). The Identity & Tenancy service owns the Identity store (credentials, tenant/user records) — the Experience API delegates to it rather than holding its own copy of user credentials.
- Authorization is role/policy-based, default-deny: every non-public endpoint requires an explicit policy (`docs/standards/backend-api-code-guidelines.md`'s Security section). MVP has exactly one role — account owner — per `mvp/scope-decisions.md`'s single-user decision; the policy model is designed to add a collaborator role later without a rewrite.

### Internal propagation (Experience API → System APIs)

A browser session cookie authenticates a request at the Experience API only — it isn't understood by, and must never be forwarded to, an internal System API. Instead:

- The Experience API, having already validated the caller's session, issues a short-lived, internally-signed JWT per outbound request to a System API, carrying `tenant_id`, `user_id`, and `role` as claims.
- Each System API verifies the token's signature and expiry and reads `tenant_id`/`user_id`/`role` from its claims — it never re-validates the original cookie and never calls the Identity & Tenancy service to authenticate. Every command and query is scoped by the `tenant_id` claim, consistent with `docs/standards/backend-api-code-guidelines.md`.
- This keeps the internal trust boundary independent of how the browser is authenticated: the signing mechanism works the same whether the System APIs are collocated with the Experience API (as they are for MVP) or later split into their own deployables — no service is exposed to a shared session cookie or a dependency on the Identity store just to authorize a request.
- Considered and rejected: trusting the internal network and forwarding plain headers (breaks the moment any service becomes reachable from outside that network); each System API independently validating the same Identity cookie (couples every service's authorization to the Identity & Tenancy service's session format and store, contradicting the database-per-service design in this document).

### Open items

- Signing-key ownership and format (a symmetric key shared via configuration vs. asymmetric signing) and rotation policy aren't decided yet — a shared symmetric key managed through Aspire configuration is sufficient to get Phase 1 working; revisit before this leaves a private pilot.
- The collaborator/multi-user role model referenced above isn't designed — only the single "owner" role is needed for MVP. Extend `domain.md`'s User entity with role/permission fields when that work starts, not before.
