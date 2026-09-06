# Contracts

This document catalogs the commands and queries each service exposes, following the CQRS conventions in `docs/standards/backend-api-code-guidelines.md` (a command changes state, a query reads state, neither type does both) and building directly on `docs/analysis/domain.md`, `docs/analysis/services.md`, and `docs/analysis/events.md`.

**Scope: MVP only.** This is a first pass covering exactly what the 11 MVP screens in `docs/analysis/mvp/screens-and-features.md` need, scoped by the cut-lines in `docs/analysis/mvp/scope-decisions.md` (EUR only, basic VAT, single project, single owner, private pilot). Full reconciliation and PDF-OCR import (decided 2026-09-06, see Open Items history below) are in MVP scope; live bank feed integration is not. As with the other analysis docs, this is meant to be extended — later phases (multi-project, collaboration, bank feed integration, document extraction, full tax compliance) get their own commands and queries added here when those phases are scoped, not invented now.

## Conventions

- **Naming:** commands are imperative (`IssueInvoice`); queries are `Get...` (single item) or `List...` (collection), matching `architecture-guidelines.md`'s Domain Events section, which expects a command to map onto a past-tense domain event (`IssueInvoice` → `InvoiceIssued`).
- **Endpoints:** each service is its own deployable (per `services.md`), so each hosts its own `/api/v1/...` root — there's no shared gateway path prefix between them. The Experience API has a separate `/api/v1/...` root of its own, screen-shaped rather than resource-shaped, and is the only thing the React frontend calls directly (per `architecture-guidelines.md`).
- **Every command** requires an idempotency key and an optimistic concurrency token on update, and is validated twice (structural, then business-invariant) per `backend-api-code-guidelines.md` — not repeated per row below.
- **Every query** is implicitly tenant-scoped from the auth context — not repeated per row below.
- **Auth context:** every command and query is authorized from claims (`tenant_id`, `user_id`, `role`) on a short-lived internal JWT issued per-request by the Experience API — see `services.md`'s Authentication & Authorization section for the full propagation design.
- **Money fields** are integer minor units, implicitly EUR, per `scope-decisions.md`.

## Identity & Tenancy

### Commands

| Command | Triggered by | Key inputs | Resulting event |
|---|---|---|---|
| `ProvisionTenant` | Manual onboarding (Gerardo provisions each pilot user — see `scope-decisions.md`'s private-pilot decision) | tenant name, owner name/email | `identity.tenant-created.v1` *(not previously defined — see Open Items)*; creates the owner User and the one auto-provisioned Project in the same transaction |
| `UpdateUserProfile` | User screen | name | *(internal domain event only, no other service needs it)* |
| `UpdateUserPreferences` | Settings screen | theme, language, notification settings | *(internal domain event only)* |
| `ChangePassword` | Settings screen | current password, new password | *(internal domain event only — handled largely by ASP.NET Core Identity per `tech-stack.md`, not bespoke logic)* |

### Queries

| Query | Triggered by | Output |
|---|---|---|
| `GetCurrentUser` | Every authenticated page load (User/Settings screens, and the Experience API's session bootstrap) | user profile, tenant ID, the one project's ID, preferences |

### Not in MVP

Invite flow, collaborator roles, Project CRUD/rename/archive/reactivate endpoints (the entity and the `identity.project-*.v1` events from `events.md` exist, but no MVP command triggers `ProjectRenamed`, `ProjectArchived`, or `ProjectReactivated` — they stay defined for the later multi-project phase), public self-serve signup.

## Financial Accounts & Ledger

### Commands

| Command | Triggered by | Key inputs | Resulting event |
|---|---|---|---|
| `LinkBankAccount` | Financial Overview / a bank accounts area | account_number, bank_name | `ledger.bank-account-linked.v1` |
| `ArchiveBankAccount` | same | bank_account_id | *(internal)* |
| `AddPaymentCardLabel` | same | label, cardholder_name, optional expiration | `ledger.card-linked.v1` |
| `RemovePaymentCardLabel` | same | card_id | *(internal)* |
| `RecordTransaction` | Transactions screen, manual entry | bank_account_id, amount, date, type | `ledger.transaction-recorded.v1` |
| `ImportTransactionsFromFile` | Financial Overview screen's CSV/Excel import with bank statement mapping | file, column mapping | one `ledger.transaction-recorded.v1` per imported row |
| `UpdateTransaction` | Transactions screen | transaction_id, fields | *(internal)* |
| `ArchiveTransaction` | Transactions screen ("delete") | transaction_id | `ledger.transaction-archived.v1` |

### Queries

| Query | Triggered by | Output |
|---|---|---|
| `ListBankAccounts` | Financial Overview | accounts + balances |
| `ListPaymentCards` | Financial Overview | card labels |
| `ListTransactions` | Transactions screen | search/filter/sort/paginated list, incl. `status` (active/archived) |
| `GetTransactionById` | Transactions screen | transaction detail |
| `ListUnreconciledTransactions` | Expenses/Revenues/Payments screens, when a user picks a Transaction to reconcile against | transactions not yet referenced by any `reconciled_transaction_id` elsewhere — this is a display convenience; the actual FK validation still happens in the consuming service against its own Transaction replica |

**Decision (2026-09-06): `DeleteTransaction` is now `ArchiveTransaction`** — since Transaction can be referenced cross-service (reconciliation, below), it's soft-deleted like Project, never hard-deleted, raising `ledger.transaction-archived.v1`.

### Not in MVP

Live bank feed integration (the Bank Feed Integration System API named in `api-led/system-apis.md`), real card tokenization or any PCI-scoped processing (cards are metadata-only per `scope-decisions.md`).

## Billing & Invoicing

### Commands

| Command | Triggered by | Key inputs | Resulting event |
|---|---|---|---|
| `CreateCounterparty` | Invoices screen (adding a customer/supplier) | type, name, tax_id, email, address | *(internal)* |
| `UpdateCounterparty` / `ArchiveCounterparty` | same | counterparty_id, fields | *(internal)* |
| `CreateInvoice` | Invoices screen | direction, counterparty_id, net_amount, tax_rate, date, due_date | `billing.invoice-issued.v1` on issue (see below — creation and issuing may be the same step for MVP, since there's no evidence a draft-editing workflow is needed for v1) |
| `IssueInvoice` | Invoices screen | invoice_id | `billing.invoice-issued.v1` |
| `VoidInvoice` | Invoices screen (draft only) | invoice_id | *(internal)* |
| `RecordPayment` | Payments screen | invoice_id, amount, date, payment_method | `billing.payment-recorded.v1`; also `billing.invoice-paid.v1` once the invoice's payments sum to its `total_amount` |
| `ReconcilePaymentWithTransaction` | Payments screen | payment_id, transaction_id (validated against this service's local Transaction replica — see `services.md`) | *(internal — sets `reconciled_transaction_id`)* |

### Queries

| Query | Triggered by | Output |
|---|---|---|
| `ListCounterparties` / `GetCounterpartyById` | Invoices screen | counterparty records |
| `ListInvoices` | Invoices screen | filterable by status, direction, type; search |
| `GetInvoiceById` | Invoices screen | invoice detail incl. payment history |
| `GenerateInvoiceDocument` | Invoices screen ("generate invoice document") | a rendered PDF — read-only, not a state change |
| `ListPayments` | Payments screen | payment records |

### Background process (not a user-invoked command)

`MarkInvoicesOverdue` — a scheduled job comparing `due_date` to the current date, raising `billing.invoice-overdue.v1`. Not exposed as an endpoint.

### Not in MVP

Facturae/SII/Verifactu compliance (`scope-decisions.md`), PDF invoice OCR import (the Document Extraction System API in `api-led/system-apis.md`).

## Bookkeeping & Planning

### Commands

| Command | Triggered by | Key inputs | Resulting event |
|---|---|---|---|
| `RecordExpense` | Expenses screen | amount, date, category, recurring, recurring_interval | `bookkeeping.expense-recorded.v1` (`import_source = manual`) |
| `UpdateExpense` / `DeleteExpense` | Expenses screen | expense_id, fields | *(internal)* |
| `ReconcileExpenseWithTransaction` | Expenses screen | expense_id, transaction_id (validated against this service's local Transaction replica — see `services.md`) | *(internal — sets `reconciled_transaction_id`)* |
| `ImportExpenseFromDocument` | Expenses screen's PDF import | uploaded PDF | calls the Document Extraction adapter (`api-led/system-apis.md`), creates a **draft** Expense (`import_source = pdf_ocr`) that doesn't count toward reports until confirmed |
| `ConfirmImportedExpense` | Expenses screen, after reviewing an OCR draft | expense_id, corrected fields if needed | `bookkeeping.expense-recorded.v1` — only raised on confirmation, not on the draft's creation |
| `RecordRevenue` | Revenues screen | amount, date, category, recurring, recurring_interval | `bookkeeping.revenue-recorded.v1` (`import_source = manual`) |
| `UpdateRevenue` / `DeleteRevenue` | Revenues screen | revenue_id, fields | *(internal)* |
| `ReconcileRevenueWithTransaction` | Revenues screen | revenue_id, transaction_id (validated against this service's local Transaction replica) | *(internal — sets `reconciled_transaction_id`)* |
| `ImportRevenueFromDocument` | Revenues screen's PDF import | uploaded PDF | calls the Document Extraction adapter, creates a **draft** Revenue (`import_source = pdf_ocr`) pending confirmation |
| `ConfirmImportedRevenue` | Revenues screen, after reviewing an OCR draft | revenue_id, corrected fields if needed | `bookkeeping.revenue-recorded.v1` — only on confirmation |
| `CreatePlan` | Plans screen | title, description, allocated_amount, start_date, end_date | `bookkeeping.plan-created.v1` |
| `UpdatePlan` / `ArchivePlan` | Plans screen | plan_id, fields | *(internal)* |
| `AddPlannedExpenseLine` | Plans screen | plan_id, amount, date, category, recurring | `bookkeeping.planned-expense-added.v1` |
| `AddPlannedRevenueLine` | Plans screen | plan_id, amount, date, category, recurring | `bookkeeping.planned-revenue-added.v1` |

### Queries

| Query | Triggered by | Output |
|---|---|---|
| `ListExpenses` / `ListRevenues` | Expenses/Revenues screens | search/filter/sort/paginated lists |
| `ListPlans` / `GetPlanById` | Plans screen | plan incl. its planned revenue/expense lines |
| `GetPlanVsActual` | Plans screen ("track plan usage and history") | planned vs. actual comparison for a plan's period, joining this service's own actuals — no cross-service call needed since Expense/Revenue and Plan are all owned here |

### Not in MVP

Approval workflows or multi-scenario forecasting on Plans. Splitting one Transaction's reconciliation across multiple Expenses/Revenues, or one Expense/Revenue across multiple Transactions (reconciliation is one-to-one for MVP — see `events.md` Open Items).

## Reporting

Unlike the other four, Reporting's queries read from projections built off the other services' integration events (per `services.md`), not from a locally-owned transactional aggregate. Its MVP surface is small but not empty — the Reports screen lets a user generate, save, edit, and delete reports, so `Report` does need a thin write surface for its own metadata (title, saved criteria), separate from the financial content, which is always computed fresh from projections rather than frozen at save time.

### Commands

| Command | Triggered by | Key inputs | Resulting event |
|---|---|---|---|
| `SaveReport` | Reports screen | title, criteria (date range, filters) | *(internal)* |
| `UpdateReport` / `DeleteReport` | Reports screen | report_id, fields | *(internal)* |

### Queries

| Query | Triggered by | Output |
|---|---|---|
| `GetFinancialOverview` | Home / Financial Overview screens | revenue/expense/plan summary and trend data for the current period |
| `ListReports` / `GetReportById` | Reports screen | saved report metadata plus freshly computed content for the saved criteria |

## Experience API

No commands or queries of its own — every endpoint here composes calls to the five services above and shapes the result for one screen. Representative endpoints, one per MVP screen: `GET /experience/v1/home`, `/financial-overview`, `/expenses`, `/revenues`, `/invoices`, `/payments`, `/transactions`, `/reports`, `/plans`, `/settings`, `/user`, plus the write-side endpoints that simply forward a command to the owning service's System API. This is the only layer the React frontend calls, per `architecture-guidelines.md`.

## Open Items

**Resolved 2026-09-06:**

1. ~~Transaction isn't linked to Invoice/Payment/Expense~~ — resolved: full reconciliation is in MVP scope. `Payment`, `Expense`, and `Revenue` each carry an optional `reconciled_transaction_id`, validated against a local Transaction replica the same way `project_id` is validated (see `services.md` and `events.md`). One-to-one only for MVP — see `events.md` Open Items for the split-reconciliation limitation.
2. ~~PDF invoice import status was ambiguous~~ — resolved: full OCR extraction is in MVP scope, implemented as an Infrastructure-layer adapter inside Bookkeeping & Planning (`api-led/system-apis.md`). Extracted records land as a draft (`import_source = pdf_ocr`) requiring explicit user confirmation (`ConfirmImportedExpense`/`ConfirmImportedRevenue`) before they count toward reports, since OCR accuracy isn't assumed to be perfect.

3. ~~`identity.tenant-created.v1` wasn't cataloged anywhere~~ — resolved: it's listed in `events.md`'s "Other Anticipated Integration Events" as a record-of-fact event (not a domain synchronization event — Tenant stays excluded from that category per `events.md`'s reasoning).

**Still open:** none — all contract-level gaps found while drafting this document are resolved as of 2026-09-06.
