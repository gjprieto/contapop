# MVP Workplan — Phases

This document defines the phase breakdown for building the Contapop MVP, following spec-driven development: each phase is implemented against the specs already established in `docs/analysis/domain.md`, `services.md`, `events.md`, and `contracts.md`, rather than against ad hoc decisions made during coding. Phases are approved here at the phase level; the next step is dividing each phase into tasks (see "Next Step" at the end).

## Methodology

Phases are sliced **vertically**, not by architectural layer. A phase delivers a complete path — database → System API → Experience API → React screen → Playwright test — for a coherent piece of the product, so that every phase is end-to-end testable through the actual frontend, not just through an API client. This deliberately departs from `architecture-guidelines.md`'s generic layer-by-layer delivery sequence (which wouldn't be UI-testable until its third step) in favor of testability at every phase boundary.

Within a phase, the first task (once phases are broken into tasks) is always to confirm the relevant spec slice is complete enough to build against — not to start coding. Later tasks implement against that fixed spec, and the phase's final task wires up its Playwright E2E coverage.

Per `docs/analysis/mvp/scope-decisions.md`'s test-rigor decision, only the money-critical flows called out per phase get full Playwright E2E coverage; everything else gets unit/integration tests. "End-to-end tested" below means the full local stack (Aspire + Postgres + Playwright), not a deployed environment — real deployment is Phase 6's job.

## Phase 1 — Foundation & Identity/Tenancy

**Depends on:** nothing (first phase).

**Delivers:**
- Shared plumbing, built once and reused by every later phase: the .NET Aspire AppHost wired to all five Postgres databases from `services.md`'s Data Storage section, the CI pipeline skeleton (build/lint/test), the Experience API skeleton, the React app skeleton (routing, TanStack Query client, auth-aware shell), and the transactional outbox pattern.
- Identity & Tenancy System API: `ProvisionTenant` (manual/admin-driven, per the private-pilot onboarding decision — not necessarily a UI form), authentication, `GetCurrentUser`, `UpdateUserProfile`, `UpdateUserPreferences`, the single auto-provisioned Project per tenant.
- Publishing `identity.project-created.v1` through the new outbox, even though no consumer exists yet.
- Frontend: login, Home screen shell (greeting only — real widgets come in Phase 5), Settings screen, User screen.

**E2E test:** log in as a seeded pilot user, see their name on Home, edit profile and preferences on the Settings/User screens, confirm changes persist after reload. A separate integration test confirms `ProvisionTenant` creates Tenant + User + Project transactionally and that the project-created event lands in the outbox.

**Spec basis:** `domain.md` (Tenant, User, Project), `services.md` (Identity & Tenancy section, Data Storage), `events.md` (`identity.project-*.v1`), `contracts.md` (Identity & Tenancy commands/queries), `mvp/scope-decisions.md` (single-owner, single-project, private-pilot decisions).

## Phase 2 — Financial Accounts & Ledger

**Depends on:** Phase 1 (needs Project to exist and its events to consume).

**Delivers:**
- The inbox side of the event backbone, built here for the first time — the first proof that a service can validate a cross-service reference (`project_id`) against a locally replicated read model instead of a synchronous call.
- Financial Accounts & Ledger System APIs: Bank Accounts (`LinkBankAccount`, `ArchiveBankAccount`), Payment Cards (`AddPaymentCardLabel`, `RemovePaymentCardLabel`, metadata-only), Transactions (`RecordTransaction`, `ImportTransactionsFromFile` for CSV/Excel, `UpdateTransaction`, `ArchiveTransaction` — soft-delete, not hard-delete, since Transaction can be referenced cross-service starting in Phase 3/4).
- Transaction's own outbox, publishing `ledger.transaction-recorded.v1` / `ledger.transaction-updated.v1` / `ledger.transaction-archived.v1` for Phase 3 and 4 to consume later.
- Frontend: Financial Overview screen's accounts/cards section, Transactions screen (full CRUD, search/filter/sort/paginate, CSV import wizard).

**E2E test:** link a bank account to the pilot project (proves the Project replica works) and confirm linking to a fabricated project ID is rejected (proves validation isn't a rubber stamp); record a transaction manually and via CSV import; archive a transaction.

**Spec basis:** `domain.md` (Bank Account, Credit/Debit Card, Transaction), `services.md` (Financial Accounts & Ledger section, updated Cross-Service Data Consistency Strategy), `events.md` (`ledger.*.v1`), `contracts.md` (Financial Accounts & Ledger section).

## Phase 3 — Billing & Invoicing

*Can run in parallel with Phase 4 once Phase 2 is done — no direct dependency between them.*

**Depends on:** Phase 1 (Project) and Phase 2 (Transaction, for reconciliation).

**Delivers:**
- Counterparty CRUD, Invoice (VAT breakdown, direction, counterparty reference), `IssueInvoice`, `VoidInvoice`, `ArchiveInvoice`, invoice filtering/details and one Blob-backed attachment, `RecordPayment`, `ReconcilePaymentWithTransaction` (second proof of the event backbone — this service replicating Financial Accounts & Ledger's Transaction), `GenerateInvoiceDocument` (PDF), the `MarkInvoicesOverdue` background job.
- Publishing `billing.invoice-issued.v1`, `billing.invoice-paid.v1`, `billing.invoice-overdue.v1`, `billing.invoice-archived.v1`, `billing.payment-recorded.v1` for Phase 5's Reporting to consume.
- Frontend: Invoices list/details screens, Payments screen.

**E2E test:** create a counterparty, issue an invoice with a VAT rate, filter and open its details, attach/view/replace/remove a source document, record a payment, reconcile that payment against a Phase 2 transaction, watch the invoice flip to paid, confirm it can no longer be archived, and download the generated PDF.

**Spec basis:** `domain.md` (Counterparty, Invoice/Ticket, Invoice Attachment, Payment), `services.md` (Billing & Invoicing section and Blob Storage), `events.md` (`billing.*.v1`), `contracts.md` (Billing & Invoicing section).

## Phase 4 — Bookkeeping & Planning

*Can run in parallel with Phase 3.*

**Depends on:** Phase 1 (Project) and Phase 2 (Transaction, for reconciliation).

**Delivers:**
- Expense and Revenue CRUD with reconciliation (`ReconcileExpenseWithTransaction`, `ReconcileRevenueWithTransaction`), the PDF-OCR import adapter and its draft/confirm flow (`ImportExpenseFromDocument`/`ImportRevenueFromDocument` + `ConfirmImportedExpense`/`ConfirmImportedRevenue`), Plan CRUD with `AddPlannedExpenseLine`/`AddPlannedRevenueLine`, `GetPlanVsActual`.
- Publishing `bookkeeping.expense-recorded.v1`, `bookkeeping.revenue-recorded.v1`, `bookkeeping.plan-created.v1`, `bookkeeping.planned-expense-added.v1`, `bookkeeping.planned-revenue-added.v1` for Phase 5.
- Frontend: Expenses screen, Revenues screen, Plans screen.

**E2E test:** record an expense manually and reconcile it against a Phase 2 transaction; upload a sample receipt PDF and confirm the resulting draft; create a plan with a planned expense line and see it reflected in plan-vs-actual.

**Spec basis:** `domain.md` (Expense, Revenue, Plan, Planned Revenue, Planned Expense), `services.md` (Bookkeeping & Planning section), `events.md` (`bookkeeping.*.v1`), `contracts.md` (Bookkeeping & Planning section), `api-led/system-apis.md` (Document Extraction adapter).

## Phase 5 — Reporting & Dashboards

**Depends on:** Phase 3 *and* Phase 4 — Reporting's projections need both services' business-fact events actually flowing before there's anything real to aggregate.

**Delivers:**
- The projection layer consuming every event published so far (Project, Transaction, Invoice/Payment, Expense/Revenue/Plan lifecycle events).
- `GetFinancialOverview`, `SaveReport`/`UpdateReport`/`DeleteReport`/`ListReports`/`GetReportById`.
- Frontend: Home screen's real widgets (replacing Phase 1's greeting-only shell), Financial Overview screen's charts/summaries, Reports screen.

**E2E test:** using data created across Phases 2–4, confirm Home and Financial Overview show correct aggregated totals; generate a report, save it, reopen it from the Reports list, and confirm the figures are consistent.

**Spec basis:** `services.md` (Reporting section), `contracts.md` (Reporting section), the full `events.md` catalog — this is the first phase where every previously defined event actually gets consumed.

## Phase 6 — Hardening & Launch Readiness

**Depends on:** all of Phases 1–5.

**Delivers:**
- The hosting decision (still open per `mvp/scope-decisions.md`) and a real deployment pipeline.
- Notification delivery for the "reminders for overdue/upcoming" language scattered across the MVP screens — in-app at minimum, email if time allows.
- A GDPR/security pass sized for a private pilot (not a public launch).
- The full critical-path Playwright regression suite run against the actual deployed environment, not just local Aspire.

**E2E test:** the full critical-path chain — link account → issue invoice → record expense → reconcile → view report — passing against the real pilot deployment.

**Spec basis:** `mvp/scope-decisions.md`'s still-open hosting/test-rigor items and the deferred Verifactu-timeline follow-up note.

## Dependency Summary

| Phase | Depends on | Can parallelize with |
|---|---|---|
| 1. Foundation & Identity/Tenancy | — | — |
| 2. Financial Accounts & Ledger | 1 | — |
| 3. Billing & Invoicing | 1, 2 | Phase 4 |
| 4. Bookkeeping & Planning | 1, 2 | Phase 3 |
| 5. Reporting & Dashboards | 3, 4 | — |
| 6. Hardening & Launch Readiness | 1–5 | — |

Phases 1, 2, and 5 are inherently serial — each needs the previous phase's data and events to test against. Phases 3 and 4 are genuinely independent of each other and are the one clear opportunity to run two implementation workstreams in parallel, per the AI-agent-driven delivery model noted in `mvp/scope-decisions.md`.

## Next Step

Each phase above gets divided into tasks, where every task is:
- A complete, independently implementable slice of that phase (not a horizontal layer within it).
- Testable and committable on its own.
- Allowed to depend on previously completed and tested tasks, but not on work that hasn't landed yet.

That task breakdown is the next document, built phase by phase from this one.
