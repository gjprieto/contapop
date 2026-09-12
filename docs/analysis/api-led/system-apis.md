# System APIs — API-Led Connectivity Analysis

This document identifies the System API layer for Contapop, following the API-led connectivity model already adopted in `docs/standards/architecture-guidelines.md`. It builds directly on `docs/analysis/domain.md` (entities) and `docs/analysis/mvp/screens-and-features.md` (screen-level capabilities).

> **Superseded for current decisions:** the four open questions this document originally raised were resolved on 2026-09-06 — see `docs/analysis/domain.md` for the entity changes and `docs/analysis/services.md` for the current service grouping and its "Domain Questions Resolved" section. This document is kept as the original System API derivation and reasoning; where it conflicts with `services.md`, `services.md` wins.

## How System APIs Were Derived

A System API provides stable, protocol-consistent access to a single system of record and owns that system's validation, authorization, and core business operations. Per the architecture guidelines, a System API maps to one bounded context / service; entities are grouped into the same System API only when they share a data owner and a release lifecycle. Reporting, cross-entity orchestration, and channel-specific shaping are deliberately excluded here — those belong to Process and Experience APIs.

Grouping the domain's 15 entities against that rule produces nine System APIs, plus two integration-facing System APIs anticipated for later phases.

## Proposed System APIs

### 1. Identity & Tenancy System API
**Owns:** Tenant, User, Project

Tenant, User, and Project share the same lifecycle (created once, rarely change, referenced everywhere else by ID) and the same concerns (auth, roles/permissions, multi-tenant isolation). Project is included here rather than split out: it's a lightweight grouping/scoping construct, not a domain with its own business rules.

Core operations: tenant CRUD and provisioning, user CRUD, role/permission assignment, project CRUD, user↔tenant membership, authentication/token issuance (or a thin wrapper if an external IdP is used).

Note: user preferences (theme, language, notifications) shown on the Settings/User screens can live here as a sub-resource rather than a separate System API.

### 2. Bank Accounts System API
**Owns:** Bank Account

Kept separate from Cards because bank account data will eventually be sourced from an external open-banking/aggregation provider (see System API #10) with its own compliance profile (PSD2/AISP), different from card data (PCI-DSS). Different security and lifecycle requirements justify the split per the architecture guidelines' service-decomposition rule.

Core operations: link/unlink bank account, CRUD account metadata, balance retrieval, list accounts by tenant/project.

### 3. Payment Cards System API
**Owns:** Credit or Debit Card

**Resolved (2026-09-06): metadata-only for the MVP.** No real card number is stored — just a user-entered label (e.g. "Visa ending 1234"). No PCI-DSS scope or tokenization vendor is needed for now; this section's original PCI-isolation rationale applies only if real card processing is added later.

Core operations: add/remove card label, list cards by tenant/project, card metadata CRUD (label, cardholder name, optional expiration — never a real card number).

### 4. Transactions System API
**Owns:** Transaction

The transaction ledger is its own system of record distinct from account/card management — it's an append-heavy stream fed by bank statement import today and, later, by a live feed from System API #10. Keeping it separate lets the ingestion/reconciliation logic evolve independently of account management.

Core operations: create transaction (manual or bulk import), query/list by account/date/type, categorize/reconcile, link transaction to invoice or payment.

### 5. Invoicing System API
**Owns:** Invoice or Ticket

Matches the "Invoicing System API" example already named in the architecture guidelines.

Core operations: CRUD invoice, transition status (draft/sent/paid/overdue), list/filter by status and direction, and manage uploaded invoice attachments. The earlier generated-invoice-document operation is superseded by `domain.md` and `contracts.md`.

**Resolved (2026-09-06):** `Counterparty` (customer/supplier) is now a first-class entity in `domain.md`, owned by this same service, referenced from Invoice via `counterparty_id` plus a `direction` field. This fills the "Clients System API" example already sitting in the architecture guidelines with nothing in the domain analysis to back it. Expense and Revenue do not reference it — `domain.md` doesn't model that link today.

### 6. Payments System API
**Owns:** Payment

Matches the architecture guidelines' example. Kept distinct from Invoicing because a payment can plausibly exist independent of a single invoice model evolving (partial payments, refunds) and may later integrate an external payment gateway/rail — same rationale as Bank Accounts/Cards.

Core operations: record payment, link to invoice/ticket, list/filter by method/date, payment status.

### 7. Expenses System API
**Owns:** Expense

Matches the architecture guidelines' example. Recurring-expense scheduling logic lives here since it's intrinsic to the entity, not a cross-cutting concern.

Core operations: CRUD expense, list/filter by category/date/recurrence, generate recurring instances.

### 8. Revenues System API
**Owns:** Revenue

Symmetric counterpart to Expenses; not explicitly named in the architecture guidelines yet but follows the same pattern and shouldn't be folded into Invoicing, since revenue can be recorded without an invoice.

Core operations: CRUD revenue, list/filter by category/date/recurrence, generate recurring instances.

### 9. Planning System API
**Owns:** Plan, Planned Revenue, Planned Expense

Forecast/plan data has a different lifecycle from actuals (Expense/Revenue/Transaction): it's forward-looking, editable, and compared against actuals rather than reconciled against a bank feed.

**Resolved (2026-09-06):** `Budget` is merged into `Plan` — `Plan` now carries an optional `allocated_amount`/`start_date`/`end_date` absorbed from the former Budget entity, matching the single "Plans" screen in the MVP.

Core operations: CRUD plan, CRUD planned revenue/expense line items under a plan, plan-vs-actual comparison data feed (consumed by a Process/Reporting API, not computed here).

## Explicitly Not a System API

### Report
`Report` doesn't own an independent system of record in the API-led sense — its content is a derived aggregation of Expenses, Revenues, Invoices, Payments, Transactions, and Plans. Per the architecture guidelines, this is Process/Reporting territory: a **Reporting Process API** (backed by cross-entity projections, as the guidelines' projection section describes) composes the other System APIs' data. If saved/named report definitions and generated report artifacts (e.g., exported PDFs) need persistence, a thin **Reports System API** could own just that metadata — but it would not compute financial content itself.

## Integration-Facing System APIs

Both MVP screens (Financial Overview, Expenses, Revenues) call out import from CSV/Excel and PDF invoice import. CSV/Excel import is a file-upload/parsing flow orchestrated within Financial Accounts & Ledger, not a separate System API. Two external system integrations are worth naming as their own boundary:

- **Bank Feed Integration System API (later phase)** — wraps a PSD2/open-banking aggregator (e.g. GoCardless Bank Account Data, Tink, Plaid) to pull live balances/transactions, feeding System API #2/#4. Still deferred — not needed for MVP.
- **Document Extraction System API (Decision 2026-09-06: in MVP scope)** — wraps an OCR/document-intelligence provider (e.g. Azure AI Document Intelligence) to extract structured data from uploaded PDF invoices/receipts, feeding the Expenses and Revenues System APIs (#7, #8). Brought forward from "later phase" into MVP per the reconciliation/import scope decisions in `docs/analysis/contracts.md`. Implemented as an Infrastructure-layer adapter inside Bookkeeping & Planning (per the Service Grouping section below), not a standalone deployable, since it currently has one consuming service. Extracted fields populate a *draft* Expense/Revenue (`import_source = pdf_ocr`) that a user must confirm before it counts toward reports — OCR accuracy isn't assumed to be perfect.

Both are classic API-led "System API as insulation layer" cases: the underlying vendor can be swapped without Process/Experience APIs or the frontend noticing.

## Summary Table

| # | System API | Owns | Notes |
|---|---|---|---|
| 1 | Identity & Tenancy | Tenant, User, Project | includes user preferences |
| 2 | Bank Accounts | Bank Account | future: fed by #10 |
| 3 | Payment Cards | Credit/Debit Card | metadata-only for MVP, no PCI scope |
| 4 | Transactions | Transaction | ledger, future: fed by #10 |
| 5 | Invoicing | Invoice/Ticket | references Counterparty via `counterparty_id` + `direction` |
| 6 | Payments | Payment | |
| 7 | Expenses | Expense | |
| 8 | Revenues | Revenue | |
| 9 | Planning | Plan, Planned Revenue, Planned Expense | Budget merged into Plan |
| — | Counterparties | Customer/Supplier | fills the "Clients System API" gap |
| — | Reports (thin, optional) | saved report metadata only | actual reporting is a Process API |
| 10 | Bank Feed Integration (future) | external bank data | wraps aggregator |
| 11 | Document Extraction (future) | external OCR | wraps document AI |

## Open Questions — Resolved 2026-09-06

1. `Budget` merged into `Plan`.
2. `Counterparty` (Customer/Supplier) added to `domain.md`, owned by Billing & Invoicing.
3. `Project` confirmed as an internal organizational grouping, distinct from Counterparty.
4. Payment Cards are in MVP scope as metadata-only (no real card number, no PCI scope).

Full reasoning for each: `docs/analysis/domain.md` (entity changes) and `docs/analysis/services.md` ("Domain Questions Resolved" section).

## Service Grouping — Implementation Recommendation

Recommendation: do not deploy one service per System API. Group by actual bond (shared data owner + shared lifecycle) and start with six deployables, not eleven. This is the same conservatism `architecture-guidelines.md`'s "Required Delivery Sequence" already applies to Process APIs and projections ("only for proven multi-service workflows") — apply it to System API decomposition too.

| # | Deployable service | System APIs hosted | Why bonded together | Split it out when |
|---|---|---|---|---|
| 1 | Identity & Tenancy Service | Identity & Tenancy | Foundational, near-zero business-rule churn, everyone else depends on it | Rarely — stays a stable platform service |
| 2 | Financial Accounts & Ledger Service | Bank Accounts, Payment Cards, Transactions | Same data owner (linked sources + their movements), same lifecycle as bank-feed integration matures; PCI risk is addressed by tokenizing cards at a vendor, not by standing up a whole isolated service | Bank-feed ingestion needs its own scaling/reliability profile, or card handling reaches real PCI Level 1 scope |
| 3 | Billing & Invoicing Service | Invoicing, Payments, Counterparties (fills the "Clients System API" gap) | Payment status derives from invoice state; both need a customer/supplier record. Keeping them together avoids two services making a synchronous round trip to close every invoice | Counterparties needs to become genuine shared reference data for other bounded contexts |
| 4 | Bookkeeping & Planning Service | Expenses, Revenues, Planning (Plan/PlannedRevenue/PlannedExpense) | Actuals vs. plan is one comparison problem with the same consumer (Financial Overview screen) and the same owning team | Planning grows real workflow (approvals, multi-scenario forecasting) independent of actuals |
| 5 | Reporting Service | none (Process/read-model layer) | Never owns write-side data — already scoped in `architecture-guidelines.md` as Application/Infrastructure/Api only, no Domain folder | N/A — stays a consumer of the others' domain events from day one |
| 6 | Experience API | none (Experience layer) | Thin, channel-specific aggregator per `architecture-guidelines.md` | One per new channel, if a second client (mobile) appears |

Within each merged service, keep every hosted System API as its own independently documented HTTP surface: own route prefix, own OpenAPI doc, own command/query handlers, own PostgreSQL schema even though it's the same database instance. No code reaches across schemas. That way, extracting Payment Cards or Transactions into their own service later is "move a schema and a folder," not a redesign — the same posture the guidelines already take toward splitting the scaffold API.

**Don't build the two integration System APIs (Bank Feed Integration, Document Extraction) as standalone deployables.** Implement each as an Infrastructure-layer adapter inside the service that consumes it — bank feed inside Financial Accounts & Ledger (later phase), document extraction inside Bookkeeping & Planning (now in MVP scope, per `contracts.md`) — until a second, independent consumer needs the same adapter. That's the proven-need trigger for pulling it out, same as the guidelines' rule for orchestrators.

**Net effect vs. one-per-System-API:** 6 deployables (each with its own pipeline, Dapr sidecar, database, and on-call surface) instead of 11+. For a small team that's the difference between a manageable release cadence and needing lockstep coordination across a dozen services for something as small as a shared currency value-object change.

This refines rather than replaces the starting service list already sketched in `architecture-guidelines.md`'s Module Structure section (`Identity, Clients, Invoicing, Expenses, Payments, Reporting`) — mainly by naming Financial Accounts & Ledger and Bookkeeping & Planning explicitly, and folding the placeholder "Clients" service into Billing & Invoicing as Counterparties. Worth updating that section once the open questions from the previous section are resolved.
