# MVP Task Breakdown

This document divides each phase in `docs/analysis/mvp/workplan.md` into tasks, following spec-driven development: every task is implemented against the specs already fixed in `domain.md`, `services.md`, `events.md`, and `contracts.md` (plus the `docs/standards/*` code guidelines for *how* to build it), not against decisions invented while coding. Per `AGENTS.md`, if a task turns up a gap or contradiction in those specs, stop and surface it rather than resolving it silently.

## How to read this document

Each task has:

- **Depends on** — tasks that must already be completed, tested, and committed. A task never depends on work that hasn't landed yet, per `workplan.md`'s task rules.
- **Implement** — what the task actually builds, naming the concrete commands/queries/entities/events/screens involved and pointing at their spec source.
- **Automated tests** — what the agent itself writes and runs before reporting the task done (unit/integration per `backend-api-code-guidelines.md` and `frontend-code-guidelines.md`; Playwright only where a task is explicitly called out as carrying E2E coverage, per the critical-paths-only test rigor in `scope-decisions.md`).
- **What you can test** — the concrete, manual thing Gerardo can do after the agent reports completion to confirm the task actually works, without needing to read the code. Where the task has no independently observable behavior yet (e.g. a database migration with nothing calling it), this says so plainly and points at the next task where it becomes observable.

Tasks are numbered `<phase>.<sequence>` (e.g. `1.3`). Sequence order within a phase is the intended build order, not a strict numbering of dependencies — dependencies are always stated explicitly.

Every service task uses the folder layout in `backend-api-code-guidelines.md`'s Service Structure section (`src/Contapop.<Service>.Service/Api|Application|Domain|Infrastructure|Tests`), applied per the six services named in `services.md`. Every frontend task uses `frontend-code-guidelines.md`'s Project Structure (`src/features/<feature>/...`).

---

## Phase 1 — Foundation & Identity/Tenancy

### Task 1.1 — Confirm the spec slice

**Depends on:** nothing.

**Implement:** no code. Re-read `domain.md` (Tenant, User, Project), `services.md` (Identity & Tenancy section, Data Storage, Authentication & Authorization), `events.md` (`identity.project-*.v1`), and `contracts.md` (Identity & Tenancy section) side by side with this task list and confirm there's nothing missing to start building — e.g. that `ProvisionTenant`'s exact request/response shape and the seeded-pilot-user assumption in the Phase 1 E2E test are both unambiguous. If something's missing or contradictory, stop and raise it instead of guessing, per `AGENTS.md` §6.

**Automated tests:** none — this is a review checkpoint, not code.

**What you can test:** review the agent's written confirmation (or the gap it raises) yourself; nothing runnable yet.

### Task 1.2 — Aspire AppHost: five databases + Redis wired up

**Depends on:** 1.1.

**Implement:** add all five PostgreSQL databases from `services.md`'s Data Storage table (`contapop_identity`, `contapop_ledger`, `contapop_billing`, `contapop_bookkeeping`, `contapop_reporting`) to `Contapop.Application.AppHost/AppHost.cs` via Aspire's `AddDatabase(...)`, alongside the existing Redis resource. No service projects consume them yet — this task only proves the AppHost can stand up the whole data layer. Remove nothing from `AppHost.cs` yet (the single `Contapop.Application.Server` project and Vite frontend stay wired as-is until Task 1.4 replaces them).

**Automated tests:** none yet (no service code exists to test against these databases). If Aspire's own health-check wiring is available, add `WithHttpHealthCheck`/readiness for each new database resource.

**What you can test:** run `dotnet run --project Contapop.Application.AppHost` from `src/` and open the Aspire dashboard — confirm all five Postgres databases (plus Redis) show up and reach a healthy/running state. Nothing else is observable yet.

### Task 1.3 — Identity & Tenancy service: EF Core schema + migration

**Depends on:** 1.2.

**Implement:** create `src/Contapop.Identity.Service/` per the Service Structure convention, with a `tenancy` schema (Tenant, Project) and a `users` schema (User) in `contapop_identity`, matching `domain.md`'s attribute lists exactly. Add the outbox table (per `architecture-guidelines.md`'s Transactional Outbox section) in this service's database. Generate and apply the first EF Core migration. No commands/endpoints yet — this task is schema only.

**Automated tests:** a migration test that applies the migration against a Testcontainers Postgres instance and asserts the expected tables/columns exist (per `backend-api-code-guidelines.md`'s Entity Framework Core section).

**What you can test:** ask the agent to show you the applied schema (e.g. `\dt` / `\d+ tenants` output against the running `contapop_identity` database via the Aspire dashboard's Postgres connection, or a quick `psql` session) and confirm it matches `domain.md`'s Tenant/User/Project attributes. Still no runnable feature.

### Task 1.4 — `ProvisionTenant` command + Identity store + outbox publish

**Depends on:** 1.3.

**Implement:** the `ProvisionTenant` command from `contracts.md` and its handler in `Application/Commands/ProvisionTenant/`. This is also where ASP.NET Core Identity's credential store is introduced (moved up from Task 1.5, 2026-09-06): add ASP.NET Core Identity's schema (via `IdentityDbContext`/`AddIdentityCore`, per `services.md`'s Authentication & Authorization section) to the Identity & Tenancy service's database and generate its migration alongside Task 1.3's schema. `ProvisionTenant` then, in one transaction, creates the domain Tenant + owner User + the one auto-provisioned Project (per `scope-decisions.md`'s single-project/single-owner/private-pilot decisions), *and* creates the owner's ASP.NET Core Identity credential record from the request's `initialPassword` (hashed via Identity's own password hasher) — this is the only place `initialPassword` is ever persisted, and it never touches the domain `User` entity. On success, write both `identity.tenant-created.v1` and `identity.project-created.v1` (per `events.md`) to the outbox in the same transaction. Expose it as a real HTTP endpoint on the Identity & Tenancy service's own `/api/v1/...` root (no Experience API yet — this is called directly for now, e.g. via a seed script or Swagger).

**Automated tests:** a command-handler unit test (fakes for persistence/time, per `backend-api-code-guidelines.md`'s Unit Testing section) plus an integration test against Testcontainers Postgres asserting Tenant + User + Project + the Identity credential record are all created in one transaction, and that both outbox rows (`identity.tenant-created.v1`, `identity.project-created.v1`) are written with the correct payload shapes from `events.md`.

**What you can test:** call the `ProvisionTenant` endpoint yourself (via the service's OpenAPI/Swagger UI, since `AddOpenApi()` is already wired per the scaffold) with a test tenant name/owner email/initial password, then query the database directly to see the Tenant, User, and Project rows land together, plus the Identity credential row for the owner, plus two rows in the outbox table.

### Task 1.5 — Authentication: cookie login + change password

**Depends on:** 1.4.

**Implement:** wire ASP.NET Core Identity's runtime authentication pieces into the Identity & Tenancy service per `services.md`'s Authentication & Authorization section — cookie authentication middleware, a login endpoint that validates against the Identity credential store Task 1.4 already created, and `ChangePassword` from `contracts.md`. No credential-storage schema work here (that's done in 1.4); this task is login/cookie/password-change behavior only. The tenant owner provisioned in Task 1.4's own testing already has a usable credential — use it as the pilot login target rather than seeding a separate one.

**Automated tests:** integration tests for login success/failure and `ChangePassword`, per the Unit Testing section's guidance on faking identity where possible and using Testcontainers where real Identity behavior must be exercised.

**What you can test:** hit the login endpoint directly (Swagger or `curl -c cookies.txt`) with a tenant owner's credentials from Task 1.4 and confirm a session cookie comes back; confirm a wrong password is rejected.

### Task 1.6 — Experience API skeleton + internal JWT issuance

**Depends on:** 1.5.

**Implement:** stand up the Experience API as its own project (per `services.md`, no data of its own). It terminates the login cookie from Task 1.5, and on every authenticated call to a downstream System API it issues the short-lived internally-signed JWT described in `services.md`'s Authentication & Authorization section (`tenant_id`/`user_id`/`role` claims), using the shared symmetric signing key noted there as sufficient for Phase 1. Add `GET /experience/v1/user` (wraps `GetCurrentUser`) as the first real Experience API endpoint, calling through to the Identity & Tenancy service with the internal JWT attached.

**Automated tests:** an integration test that logs in, calls `/experience/v1/user`, and asserts the Identity & Tenancy service received a validly-signed internal JWT with the right claims (not the raw cookie).

**What you can test:** log in through the Experience API (not the Identity service directly this time), then call `GET /experience/v1/user` with the resulting session and see your seeded pilot user's profile come back.

### Task 1.7 — `UpdateUserProfile` / `UpdateUserPreferences` + their queries

**Depends on:** 1.6.

**Implement:** the remaining Identity & Tenancy commands from `contracts.md` (`UpdateUserProfile`, `UpdateUserPreferences`) and expose them through the Experience API alongside `GetCurrentUser`.

**Automated tests:** command-handler unit tests plus an integration test round-tripping an update through the Experience API and confirming `GetCurrentUser` reflects it.

**What you can test:** call the Experience API to update your profile name and preferences (theme/language/notifications), then call `GetCurrentUser` again and see the changes reflected.

### Task 1.8 — React app skeleton: routing, TanStack Query client, auth-aware shell

**Depends on:** 1.6 (needs a real login endpoint to authenticate against).

**Implement:** per `frontend-code-guidelines.md`, install and wire React Router, TanStack Query, and the Experience API client in `shared/api`. Build the app shell: a login page, and a protected-route wrapper that redirects to login when unauthenticated. No real screens yet beyond login.

**Automated tests:** Vitest/RTL tests for the protected-route redirect behavior and the login form's validation states, per `frontend-code-guidelines.md`'s Testing section.

**What you can test:** open the running frontend in a browser, confirm visiting any page while logged out redirects to login, and confirm logging in with the seeded pilot user's credentials gets you into the (still mostly empty) app shell.

### Task 1.9 — Home shell, Settings screen, User screen

**Depends on:** 1.7, 1.8.

**Implement:** the Home screen shell (greeting only, per `workplan.md` — real widgets are Phase 5), the Settings screen (preferences from Task 1.7), and the User screen (profile from Task 1.7), per their feature lists in `screens-and-features.md`, each as its own `features/<name>/` slice.

**Automated tests:** component tests for each screen's form validation and TanStack Query cache updates on save.

**What you can test:** log in as the pilot user in the browser; see your name greeted on Home; edit your profile on the User screen and your preferences on Settings; reload the page and confirm both changes persisted.

### Task 1.10 — Phase 1 Playwright E2E + CI skeleton

**Depends on:** 1.9.

**Implement:** the Phase 1 Playwright test from `workplan.md` (log in as a seeded pilot user, see their name on Home, edit profile and preferences, confirm persistence after reload), run against the full local Aspire stack. Add the CI pipeline skeleton (build/lint/test) referenced in `workplan.md`'s Phase 1 deliverables, wired to run this suite plus every unit/integration test from Tasks 1.3–1.9.

**Automated tests:** the Playwright spec itself, plus confirming CI runs the full existing test suite green.

**What you can test:** run the Playwright suite locally yourself and watch it execute the same login → edit → reload flow headlessly; check that the CI pipeline (however it's triggered — ask the agent how to run it locally if it's not yet pushed anywhere) passes on the current branch.

---

## Phase 2 — Financial Accounts & Ledger

### Task 2.1 — Confirm the spec slice

**Depends on:** 1.10.

**Implement:** no code. Re-read `domain.md` (Bank Account, Credit/Debit Card, Transaction), `services.md`'s updated Cross-Service Data Consistency Strategy, `events.md` (`identity.project-*.v1` as consumed here, `ledger.*.v1` as produced here), and `contracts.md`'s Financial Accounts & Ledger section. Confirm the inbox/replica design for Project is concrete enough to build (what the local replica table looks like, exactly which fields it needs).

**Automated tests:** none.

**What you can test:** review the agent's confirmation or raised gap.

### Task 2.2 — Financial Accounts & Ledger service: schema + Project inbox/replica

**Depends on:** 2.1.

**Implement:** create `src/Contapop.Ledger.Service/` with `bank_accounts`, `payment_cards`, and `transactions` schemas in `contapop_ledger`, per `domain.md`. Add the **inbox** table and a local `project_replica` read table (per `services.md`'s Cross-Service Data Consistency Strategy), plus the subscriber that consumes `identity.project-created.v1`/`-renamed.v1`/`-archived.v1`/`-reactivated.v1` (per `events.md`) and projects them into `project_replica`, deduplicated by `event_id` and version-gated. This is the first proof of the inbox side of the event backbone described in `workplan.md`.

**Automated tests:** an integration test that publishes a fabricated `identity.project-created.v1` message and asserts a row lands in `project_replica`; a duplicate-delivery test asserting the same `event_id` is a no-op the second time; an out-of-order test asserting an older `aggregate_version` is ignored.

**What you can test:** with Task 1.4's `ProvisionTenant` still creating real projects and publishing to the Identity & Tenancy outbox, confirm (via direct DB query, or a small script the agent provides) that a project created there shows up in `contapop_ledger`'s `project_replica` table shortly after — this is your first real look at the event backbone actually working end-to-end between two services.

### Task 2.2a — Identity outbox dispatch and Ledger event delivery

**Depends on:** 2.2.

**Implement:** close the producer-side half of the event backbone that Task 2.2 needs but does not own: add an Identity & Tenancy outbox dispatcher that publishes pending messages through Dapr pub/sub to the event topic defined in `events.md`, run Identity & Tenancy and Financial Accounts & Ledger with Dapr sidecars, and subscribe Ledger to `identity.events`. The dispatcher claims pending rows safely, marks an event dispatched only after Dapr acknowledges publication, and records failed attempts for retry. Ledger continues to own inbox deduplication and Project replica projection from Task 2.2; no synchronous Identity-to-Ledger database or HTTP dependency is introduced.

**Automated tests:** `scripts/test-event-backbone.ps1` starts the local Dapr-capable environment, provisions a tenant, and asserts its Project reaches Ledger's `project_replica`. Dispatcher integration tests cover successful dispatch and failed-publication retry state; Task 2.2's duplicate-delivery test remains the redelivery assertion proving the inbox leaves the final replica state unchanged.

**What you can test:** provision a tenant through Identity & Tenancy, then query `contapop_ledger`'s `project_replica` table shortly afterward and see the provisioned Project without manually posting an event.

### Task 2.3 — Bank Accounts & Payment Cards commands/queries

**Depends on:** 2.2a.

**Implement:** `LinkBankAccount`, `ArchiveBankAccount`, `AddPaymentCardLabel`, `RemovePaymentCardLabel`, `ListBankAccounts`, `ListPaymentCards` from `contracts.md`. `LinkBankAccount` validates `project_id` against `project_replica` from Task 2.2 (not a synchronous call) and rejects a fabricated project ID. Publish `ledger.bank-account-linked.v1` / `ledger.card-linked.v1` per `events.md`.

**Automated tests:** command-handler unit tests for the validation rule (accept real project, reject fabricated one); integration tests for the full command → outbox path.

**What you can test:** via Swagger/curl against the Ledger service directly, link a bank account to your seeded pilot project and confirm it succeeds; try linking one to a made-up project ID and confirm it's rejected with a clear error; add and remove a payment card label.

### Task 2.4 — Transactions: record, update, archive (soft-delete)

**Depends on:** 2.2a.

**Implement:** `RecordTransaction`, `UpdateTransaction`, `ArchiveTransaction`, `ListTransactions`, `GetTransactionById`, `ListUnreconciledTransactions` from `contracts.md`. `ArchiveTransaction` sets `status = archived` (soft-delete, per `domain.md`'s Decision note — never a hard delete, since Transaction becomes cross-service-referenceable starting Phase 3). Transaction's own outbox publishes `ledger.transaction-recorded.v1` / `ledger.transaction-updated.v1` / `ledger.transaction-archived.v1` per `events.md`, ready for Phase 3/4 to consume later (no consumer exists yet).

**Automated tests:** unit tests for the archive-not-delete invariant; integration tests for the outbox rows on record/archive.

**What you can test:** record a transaction against your linked bank account, list/search/filter/sort/paginate transactions, archive one and confirm it disappears from the default active list but its row still exists with `status = archived` (ask the agent to show you via a direct query or a "show archived" filter if one exists).

### Task 2.5 — CSV/Excel transaction import

**Depends on:** 2.4.

**Implement:** `ImportTransactionsFromFile` from `contracts.md` — file upload, column-mapping UI-facing contract, one `ledger.transaction-recorded.v1` per imported row.

**Automated tests:** integration test importing a fixture CSV and asserting the right number of transactions and outbox events are created; a malformed-file test asserting a clear validation error rather than a partial import.

**What you can test:** upload a small sample CSV/Excel bank statement (ask the agent for a fixture file or provide your own) through the import endpoint and confirm the resulting transactions list matches the file.

### Task 2.5a — Global Transaction reconciliation claims

**Depends on:** 2.4.

**Implement:** add the Ledger-owned `Transaction Reconciliation Claim` aggregate and its schema, migration, internal System API contract, expiry recovery signal, and a Reconciliation Process API with its own durable reconciliation-operation retry record described in `services.md`'s **Strict global Transaction reconciliation** section and `contracts.md`. `ReserveTransactionReconciliation` atomically reserves one active Transaction for one Payment, Expense, or Revenue; `ConfirmTransactionReconciliation` makes that claim permanent; `ReleaseTransactionReconciliation` is idempotent and releases only a durably known failed reservation or a claim whose Expense/Revenue was deleted. The Reconciliation Process API owns reserve → dependent write → confirm/release orchestration while the Experience API remains stateless. An expired reservation is resolved by replaying/checking the idempotent dependent write, then confirmed or released; it is never automatically released while the outcome is unknown. Billing and Bookkeeping validate a claim through Ledger as part of their reconciliation commands. This is a narrow, explicit exception to asynchronous replica validation, not a cross-service database dependency.

**Automated tests:** Ledger integration tests covering atomic contention (only one reservation can succeed), retrying the same reservation idempotently, archived-Transaction rejection, confirmation, and explicit release. Experience API integration tests cover a successful reserve → dependent write → confirm flow, confirmation retry after an uncertain response, and a failed dependent write whose release is retried durably. An expired reservation test proves it remains blocked until the durable coordinator resolves the idempotent dependent outcome. The future Billing/Bookkeeping reconciliation tests assert that a missing or mismatched claim is rejected.

**What you can test:** reserve a Transaction for one test dependent ID, confirm a second reservation for a different dependent ID is rejected, release the first claim, then confirm the second reservation succeeds. Trigger a failed dependent write and verify the retry record releases the reservation rather than leaving the Transaction unavailable.

### Task 2.6 — Experience API + frontend: Financial Overview accounts/cards, Transactions screen

**Depends on:** 2.3, 2.4, 2.5, 2.5a, 1.8.

**Implement:** Experience API endpoints composing the above for the Financial Overview screen's accounts/cards section and the Transactions screen (full CRUD, search/filter/sort/paginate, CSV import wizard), per `screens-and-features.md`. Frontend slices in `features/accounts/` and `features/transactions/`.

**Automated tests:** component tests for the Transactions list/filter/import-wizard states; an Experience API integration test per composed endpoint.

**What you can test:** in the browser, link a bank account and add a card label from the Financial Overview screen; go to the Transactions screen, record a transaction manually, import a CSV, search/filter/sort the list, and archive a transaction.

### Task 2.7 — Phase 2 Playwright E2E

**Depends on:** 2.6.

**Implement:** the Phase 2 Playwright test from `workplan.md`: link a bank account to the pilot project (proves the replica works) and confirm linking to a fabricated project ID is rejected through the UI; record a transaction manually and via CSV import; archive a transaction.

**Automated tests:** the Playwright spec itself.

**What you can test:** run the Playwright suite and watch it execute this flow; then repeat it yourself once by hand in the browser as a sanity check.

---

## Phase 3 — Billing & Invoicing

*Can be built in parallel with Phase 4 by a separate agent/workstream once Phase 2 is done, per `workplan.md`.*

### Task 3.1 — Confirm the spec slice

**Depends on:** 2.7, 2.5a.

**Implement:** no code. Re-read `domain.md` (Counterparty, Invoice/Ticket, Payment), `services.md` (Billing & Invoicing section and **Strict global Transaction reconciliation**), `events.md` (the now-complete `billing.*.v1` payload contracts, and the Transaction replica this service needs), and `contracts.md` (Billing & Invoicing plus the Transaction Reconciliation Claim protocol). Confirm the settled design: Billing validates Transaction existence/status from its local replica, validates a Ledger-issued claim during reconciliation, and the Experience API coordinates reserve → persist → confirm or compensating release.

**Automated tests:** none.

**What you can test:** review the agent's confirmation or raised gap.

### Task 3.2 — Billing & Invoicing service: schema + Project/Transaction inbox

**Depends on:** 3.1.

**Implement:** create `src/Contapop.Billing.Service/` with `invoicing`, `payments`, and `counterparties` schemas in `contapop_billing`. Add its inbox plus **two** local replicas per `services.md`'s updated Cross-Service Data Consistency Strategy: `project_replica` (consuming `identity.project-*.v1`) and `transaction_replica` (consuming `ledger.transaction-recorded.v1`/`-updated.v1`/`-archived.v1` from Task 2.4's outbox) — this service's second proof of the event backbone, and the first case of a service consuming from two different upstream services.

**Automated tests:** the same replica correctness tests as Task 2.2 (dedup, version-gating), run against both replicas.

**What you can test:** confirm (via direct DB query) that a project from Phase 1 and a transaction recorded in Phase 2 both show up in this service's local replicas shortly after being created/recorded upstream.

### Task 3.3 — Counterparty CRUD

**Depends on:** 3.2.

**Implement:** `CreateCounterparty`, `UpdateCounterparty`, `ArchiveCounterparty`, `ListCounterparties`, `GetCounterpartyById` from `contracts.md`.

**Automated tests:** command-handler unit tests and CRUD integration tests.

**What you can test:** via Swagger/curl, create a counterparty (customer or supplier), edit it, list counterparties, archive one.

### Task 3.4 — Invoice issuing + VAT breakdown

**Depends on:** 3.3.

**Implement:** `CreateInvoice`/`IssueInvoice`, `VoidInvoice`, `ListInvoices`, `GetInvoiceById` from `contracts.md`, validating `counterparty_id` (local FK) and `project_id` (against `project_replica`). Enforces the `net_amount`/`tax_rate`/`tax_amount`/`total_amount` breakdown from `domain.md`. Publishes `billing.invoice-issued.v1` per `events.md`.

**Automated tests:** unit tests for the VAT total calculation and for rejecting a fabricated project ID; integration test for issue → outbox.

**What you can test:** create a counterparty, issue an invoice against it with a VAT rate, confirm the computed tax/total amounts are correct, list invoices filtered by status/direction/type, void a draft invoice.

### Task 3.5 — Payments + reconciliation against Transaction

**Depends on:** 3.4, 3.2, 2.5a.

**Implement:** `RecordPayment` (publishes `billing.payment-recorded.v1`, and `billing.invoice-paid.v1` once payments sum to `total_amount`), `ReconcilePaymentWithTransaction` (validates `transaction_id` against this service's `transaction_replica` from Task 3.2 and its matching Ledger claim from Task 2.5a — the second proof-point named in `workplan.md`), `ListPayments`, `GenerateInvoiceDocument` (PDF) from `contracts.md`.

**Automated tests:** unit test for the invoice-paid threshold logic; unit test rejecting reconciliation against a fabricated/archived transaction ID or missing/mismatched reconciliation claim; integration test for the PDF generation producing a well-formed file.

**What you can test:** record a payment against your issued invoice, reconcile it against a Phase 2 transaction, watch the invoice status flip to paid once fully covered, download the generated invoice PDF and open it.

### Task 3.6 — `MarkInvoicesOverdue` background job

**Depends on:** 3.4.

**Implement:** the scheduled job from `contracts.md` comparing `due_date` to the current date and raising `billing.invoice-overdue.v1`. Not an endpoint.

**Automated tests:** a unit test with a fixed/injected clock proving an invoice past its due date and not fully paid gets marked overdue and one that isn't doesn't.

**What you can test:** ask the agent to trigger the job manually (a test hook or admin endpoint, since it's not user-invoked) against an invoice with a back-dated due date, and confirm it flips to overdue.

### Task 3.7 — Experience API + frontend: Invoices screen, Payments screen

**Depends on:** 3.4, 3.5, 3.6, 1.8.

**Implement:** Experience API endpoints for the Invoices and Payments screens, per `screens-and-features.md`. Frontend slices in `features/invoices/` and `features/payments/`.

**Automated tests:** component tests for invoice creation form validation (VAT calculation display) and payment recording/reconciliation UI states; Experience API integration tests per endpoint.

**What you can test:** in the browser, create a counterparty and issue an invoice, see it listed with correct status/type/direction filters, record a payment against it, reconcile that payment against a transaction from Phase 2, see the invoice flip to paid, download the PDF.

### Task 3.7a — Generated-document row action icon

**Depends on:** 3.7.

**Implement:** replace the invoice row's text-labelled `PDF` action with the established document icon while preserving `GenerateInvoiceDocument` behavior exactly: fetch the generated PDF and open it in a new browser tab. Give the icon-only control an accessible name and tooltip such as "Open generated invoice PDF"; this is not the uploaded attachment introduced in Task 3.7e.

**Automated tests:** update the invoice-list component test to locate the control by accessible name and assert that activating it opens the generated PDF in a new tab.

**What you can test:** click the document icon on an invoice row and confirm the generated PDF still opens in another browser tab.

### Task 3.7b — Invoice list filtering

**Depends on:** 3.7.

**Implement:** extend Billing's `ListInvoices`, its Experience API pass-through, and the invoice frontend query/types with the filtering contract in `contracts.md`: status, direction, type, inclusive `dateFrom`/`dateTo`, inclusive `totalAmountMinMinor`/`totalAmountMaxMinor`, search, date/amount sort, and pagination. Reuse the Transactions screen's URL-backed filter layout and behavior; expose EUR amount inputs converted to integer minor units at the API boundary, include every filter in the TanStack Query key, reset to page 1 when a filter changes, and add an explicit clear-filters action that restores the default non-archived list.

**Automated tests:** Billing integration tests for combined status/date/total-amount bounds and archived-by-default exclusion; frontend component tests for URL/query synchronization, minor-unit conversion, page reset, filtered results, and clearing all filters.

**What you can test:** combine status, invoice-date, and total-amount filters; sort and paginate the matching rows; then clear filters and see the default full non-archived invoice list.

### Task 3.7c — Archive invoices

**Depends on:** 3.7b.

**Implement:** add the `archived` Invoice status and `ArchiveInvoice` command from `domain.md`/`contracts.md`, including its migration, Experience API pass-through, and `billing.invoice-archived.v1` outbox event. This is a soft removal for `issued`, `overdue`, and `void` invoices only: retain the invoice and lines, exclude archived invoices from the default list, and reject archiving when the invoice is `draft`, `paid`, or has any Payment record. Return the server-computed `canArchive` flag from invoice list/details queries so the frontend does not duplicate the payment-eligibility rule. Add a row action alongside Issue/Void only for eligible invoices, require an accessible confirmation dialog, invalidate invoice list/detail queries after success, and ensure the visible count refreshes. Task 3.7e extends this flow to clean up an existing attachment; Task 3.7g adds the separate permanent-removal path for accidental drafts.

**Automated tests:** domain tests for eligible statuses and rejection of paid/payment-linked invoices; integration tests proving soft deletion, default-list exclusion, idempotency, and the outbox event; component tests for action visibility, cancel/confirm behavior, and query invalidation.

**What you can test:** archive an issued, overdue, or void invoice with no payments after confirming, verify it leaves the default list but appears under the archived filter, and verify a draft or an invoice with a payment has no archive action and is rejected by the API.

### Task 3.7d — Invoice details view

**Depends on:** 3.7c.

**Implement:** add a dedicated `/invoices/:invoiceId` frontend route backed by the existing `GetInvoiceById` System/Experience API contract. Add an eye-icon row action alongside Issue/Void/Archive with an accessible name. Display counterparty, direction, type, dates, status, every line item, net/VAT/total amounts, payment history and reconciliation state, plus attachment metadata once Task 3.7e lands. Provide an explicit return to the invoice list that preserves its URL-backed filters. Use a dedicated invoice-detail TanStack query and invalidate/update it after invoice status, payment, or attachment mutations so it never remains stale after a successful change.

**Automated tests:** component tests for the complete details rendering, loading/not-found/error states, filter-preserving back navigation, and cache refresh after a status/payment mutation; retain the existing Experience API integration coverage for `GetInvoiceById` and extend it only if the response mapping is incomplete.

**What you can test:** open an invoice through the eye icon, inspect all lines/totals/status/payment history, change its status or payment state, confirm the details refresh, and return to the same filtered invoice list.

### Task 3.7e — Invoice attachment

**Depends on:** 3.7d.

**Implement:** add the one-per-invoice `InvoiceAttachment` entity and migration plus `AttachInvoiceDocument`, `RemoveInvoiceDocument`, and `GetInvoiceAttachment` from `domain.md`/`contracts.md`. Add an Aspire Azure Storage resource configured to use Azurite locally and inject its private Billing-owned Blob container into Billing; production uses Azure Blob Storage. Store only tenant-scoped metadata/reference data in Billing PostgreSQL. Accept PDF, PNG, or JPEG up to 10 MB, validating declared type and file signature. Add a paperclip row action and details-view controls to upload, open, replace, or remove the attachment; distinguish it clearly from the generated invoice PDF. Replacements must make the new blob/metadata durable before scheduling old-blob cleanup, removals are idempotent, cleanup failures are retried, and all blob operations enforce the authenticated tenant. Extend `ArchiveInvoice` to remove an existing attachment through the same durable cleanup path.

**Automated tests:** migration and Billing integration tests using Azurite for upload/download/replace/remove, tenant isolation, invalid signature/type, empty file, over-10-MB rejection, and archive cleanup; frontend component tests for the paperclip action, attachment metadata, validation feedback, and replace/remove confirmation/cache refresh.

**What you can test:** attach a PDF, PNG, or JPEG from the row action; view it in invoice details; replace and remove it; verify unsupported or oversized files are rejected and the generated-PDF document action remains independent.

### Task 3.7f — Specify draft invoice removal

**Depends on:** 3.7e.

**Implement:** no code. Change the Invoice removal rules so a `draft` invoice with no Payment records can be permanently removed, while `issued`, `overdue`, and `void` invoices continue to use `ArchiveInvoice`; `paid` or payment-linked invoices remain non-removable. Define the `DeleteDraftInvoice` command, its System and Experience API routes, required idempotency/concurrency headers, response and error behavior, attachment/blob cleanup guarantee, and whether an integration event is required. Update every affected authoritative specification: `domain.md`, `services.md`, `events.md`, `contracts.md`, `screens-and-features.md`, `scope-decisions.md`, and `workplan.md` as applicable. Confirm that Reporting never receives a draft invoice projection, or define the required deletion event if that assumption is no longer true.

**Automated tests:** none; this task is complete only when the specification changes are internally consistent and make Task 3.7g independently implementable.

**What you can test:** review the approved contract and verify that a user can distinguish deleting an accidental draft from archiving an invoice that has entered the financial workflow.

### Task 3.7g — Delete draft invoices

**Depends on:** 3.7f.

**Implement:** implement `DeleteDraftInvoice` exactly as specified by Task 3.7f, including Billing persistence, transactional handling of invoice-line deletion and attachment/blob cleanup, the System API endpoint, Experience API pass-through, and frontend row action with an accessible confirmation dialog. The command must only permanently delete a `draft` invoice with no Payment records; all other statuses and payment-linked invoices must be rejected. Retain `ArchiveInvoice` for `issued`, `overdue`, and `void` invoices. Invalidate invoice list/detail queries after successful deletion and refresh the visible count.

**Automated tests:** domain and Billing integration tests for draft-only eligibility, payment-linked rejection, idempotent retry behavior, invoice-line and attachment cleanup, and atomic persistence behavior; Experience API forwarding coverage; frontend component tests for draft-only action visibility, cancellation, confirmation, and list/detail cache refresh. Extend the Phase 3 Playwright flow with confirmed deletion of a separate accidental draft.

**What you can test:** create an accidental draft invoice, delete it after confirming, and verify that it no longer appears under any invoice filter. Confirm that an issued, overdue, void, paid, or payment-linked invoice cannot use the delete action.

### Task 3.7h — Specify multi-line draft invoice creation and editing

**Depends on:** 3.7d.

**Implement:** no code. Define `UpdateDraftInvoice` in the authoritative specifications. Confirm that `counterpartyId`, `direction`, and `type` are immutable after creation; a draft's invoice date, due date, and complete line-item collection are mutable; submitted lines replace the complete persisted collection atomically; every Invoice has at least one line; totals use the established banker's-rounding rule; only drafts are editable; and no integration event is needed because drafts have not entered Reporting. Specify the frontend requirements for multi-line creation/editing, a draft-only details Edit action, a dedicated prepopulated edit route, cancellation without persistence, and query refresh after saving.

**Automated tests:** none; this task is complete only when `domain.md`, `contracts.md`, screen scope, workplan, and this task breakdown are internally consistent and make Task 3.7i independently implementable.

**What you can test:** review the approved specification and verify it clearly distinguishes immutable draft identity fields from editable dates and item lines, defines replacement semantics, and defines the draft-only UI flow.

### Task 3.7i — Implement multi-line draft invoice creation and editing

**Depends on:** 3.7h.

**Implement:** implement `UpdateDraftInvoice` exactly as specified by Task 3.7h, including Billing persistence, atomic replacement of Invoice Lines, VAT and aggregate-total recomputation, idempotency, optimistic concurrency, the System API endpoint, and Experience API pass-through. Update the invoice creation form to support adding and removing one or more item lines, with live calculated line and aggregate net/VAT/total amounts. Add a draft-only Edit action to the invoice details view and a dedicated prepopulated edit form that displays `counterpartyId`, direction, and type as immutable context while allowing invoice date, due date, and line items to change. Cancel returns to the detail view without a mutation; save invalidates invoice list and detail queries and returns to refreshed details. Reject all updates to non-draft invoices and never publish an integration event for a draft edit.

**Automated tests:** domain and Billing integration tests for draft-only authorization, immutable-field exclusion, complete atomic line replacement, total recomputation, idempotency, and optimistic-concurrency rejection. Experience API forwarding coverage. Frontend component tests for multi-line creation and editing, add/edit/remove line interactions, live calculated totals, draft-only edit visibility, cancellation without an API request, successful save/cache refresh, and non-draft action absence.

**What you can test:** create an invoice with multiple items and verify the displayed net/VAT/total. Open a draft invoice, edit its dates and line collection, save, and confirm the details and list totals refresh. Confirm the Edit action is absent after issuing, voiding, paying, overdue transition, or archiving.

### Task 3.7j — Specify uploaded invoice documents and other attachments

**Depends on:** 3.7a, 3.7e.

**Implement:** no code. Remove generated-invoice-PDF behavior from the MVP specification: registering or issuing an Invoice must not generate a document, and `GenerateInvoiceDocument` must be removed from the System and Experience API contracts. Replace the current one-per-invoice `InvoiceAttachment` model with an attachment collection in which an Invoice has zero or one attachment of type `invoice` and zero or more attachments of type `other`. Before the user selects a file while no `invoice` attachment exists, require them to choose `Invoice` or `Other type of attachment`; persist that choice with the attachment metadata. Once an `invoice` attachment exists, subsequent uploads must be recorded as `other` without prompting for a type. If the `invoice` attachment is removed, prompt for a type again on the next upload. The existing document-icon row action must open the uploaded `invoice` attachment in a new browser tab and must be hidden when no `invoice` attachment exists. Other attachments remain manageable from invoice details and must not make that row action visible. Define the revised upload, download, replace, and removal routes and responses, including how a specific attachment is addressed; retain tenant isolation, file validation, Blob cleanup, idempotency, and archive/delete cleanup guarantees. Update every affected authoritative specification: `domain.md`, `services.md`, `events.md`, `contracts.md`, `screens-and-features.md`, `scope-decisions.md`, and `workplan.md` as applicable. Also update this task breakdown and `20261209-new-specifications.md` so no generated-PDF or single-attachment requirement remains.

**Automated tests:** none; this task is complete only when the documentation consistently defines the attachment collection, its type-selection state machine, and the conditional document-icon behavior, making the follow-up implementation independently implementable.

**What you can test:** review the approved specification and verify these cases are unambiguous: no attachment means the document icon is hidden; first upload requires a type; uploading an invoice document makes the icon open that file; later uploads need no type choice and are other attachments; removing the invoice document hides the icon and restores the type choice for the next upload.

### Task 3.7k — Implement uploaded invoice documents and other attachments

**Depends on:** 3.7j.

**Implement:** implement the attachment model and UI specified by Task 3.7j. Remove generated-invoice-PDF generation and its System/Experience API endpoints. Migrate Billing from one attachment per invoice to a collection that permits zero or one `invoice` attachment and zero or more `other` attachments, with each attachment's type persisted and the invoice-type uniqueness enforced transactionally. Implement the revised attachment upload, download, replace, and removal contracts, addressing each attachment explicitly and preserving authenticated tenant isolation, PDF/PNG/JPEG signature and size validation, idempotent removal, and durable Blob cleanup. Update archive and draft-deletion cleanup to remove every attachment and blob. In the frontend, prompt for `Invoice` or `Other type of attachment` before file selection whenever the invoice has no invoice attachment; after one exists, upload later documents as `other` without a type prompt; restore the prompt after it is removed. Replace the generated-PDF document-icon action with a conditional action that opens the uploaded invoice attachment in a new tab and is absent when none exists. Display and manage all attachments from invoice details, without exposing other attachments through the row action. Invalidate invoice list/detail queries after every attachment mutation.

**Automated tests:** Billing migration and integration tests for attachment-type persistence, one-invoice-attachment enforcement, multiple other attachments, type-selection transitions after removal, tenant isolation, file validation, download/replace/remove behavior, and archive/draft-deletion cleanup. Experience API forwarding coverage. Frontend component tests for the type prompt, conditional document-icon visibility and browser-tab opening, automatic other-attachment uploads, attachment management, confirmation, validation feedback, and cache refresh. Update the Phase 3 Playwright flow to cover these scenarios and confirm no generated PDF endpoint is invoked.

**What you can test:** attach an `Other type of attachment` document first and confirm the document icon remains hidden; then attach an `Invoice` document and open it from the icon in a new tab. Upload another document and confirm it is automatically classified as other without a prompt. Remove the invoice document, confirm the icon disappears, then upload another document and confirm the type choice is offered again.

### Task 3.8 — Phase 3 Playwright E2E

**Depends on:** 3.7a, 3.7b, 3.7c, 3.7d, 3.7e, 3.7f, 3.7g, 3.7i, 3.7j, 3.7k.

**Implement:** the Phase 3 Playwright test from `workplan.md`: create a counterparty and multi-line draft invoice; edit its dates and line collection and confirm recalculated totals; filter and open its details; attach an other document first and confirm the document icon is hidden; attach an invoice document and open it through the icon; upload another document without a type prompt; remove the invoice document and confirm the type prompt returns; issue the invoice; record and reconcile a payment against a Phase 2 transaction; watch the invoice flip to paid; and confirm archive is unavailable. Include a second payment-free invoice to exercise confirmed archival and the archived filter, plus a separate accidental draft to exercise confirmed permanent deletion.

**Automated tests:** the Playwright spec itself.

**What you can test:** run the Playwright suite and watch this flow execute; repeat it once by hand in the browser.

---

## Phase 4 — Bookkeeping & Planning

*Can be built in parallel with Phase 3 by a separate agent/workstream once Phase 2 is done, per `workplan.md`.*

### Task 4.1 — Confirm the spec slice

**Depends on:** 2.7, 2.5a.

**Implement:** no code. Re-read `domain.md` (Expense, Revenue, Plan, Planned Revenue, Planned Expense), `services.md` (Bookkeeping & Planning section), `events.md` (`bookkeeping.*.v1`), `contracts.md` (Bookkeeping & Planning section), and `api-led/system-apis.md`'s Document Extraction adapter description. Confirm what the OCR adapter's actual interface/provider is (this hasn't been pinned down anywhere yet — flag it here rather than picking a vendor mid-task).

**Automated tests:** none.

**What you can test:** review the agent's confirmation or raised gap — this is the most likely phase to surface a real open question (the OCR provider choice), so expect this checkpoint to matter more than the others.

### Task 4.2 — Bookkeeping & Planning service: schema + Project/Transaction inbox

**Depends on:** 4.1.

**Implement:** create `src/Contapop.Bookkeeping.Service/` with `expenses`, `revenues`, and `planning` schemas in `contapop_bookkeeping`. Add its inbox plus `project_replica` and `transaction_replica` (same pattern as Task 3.2, including `ledger.transaction-recorded.v1`/`-updated.v1`/`-archived.v1`).

**Automated tests:** same replica correctness tests as Task 3.2.

**What you can test:** confirm via direct DB query that projects and transactions show up in this service's replicas.

### Task 4.3 — Expense & Revenue CRUD + reconciliation

**Depends on:** 4.2, 2.5a.

**Implement:** `RecordExpense`/`UpdateExpense`/`DeleteExpense`, `RecordRevenue`/`UpdateRevenue`/`DeleteRevenue`, `ReconcileExpenseWithTransaction`/`ReconcileRevenueWithTransaction`, `ListExpenses`/`ListRevenues` from `contracts.md`. Reconciliation validates both the local Transaction replica and its matching Ledger claim from Task 2.5a. `import_source = manual` on all records created here. Publishes `bookkeeping.expense-recorded.v1` / `bookkeeping.revenue-recorded.v1` per `events.md`.

**Automated tests:** unit tests for the reconciliation validation (reject fabricated/archived transaction or missing/mismatched reconciliation claim); integration tests for CRUD + outbox.

**What you can test:** via Swagger/curl, record an expense and a revenue manually, edit and delete one, reconcile an expense against a Phase 2 transaction.

### Task 4.4 — PDF-OCR import + draft/confirm flow

**Depends on:** 4.3, 4.1 (needs the OCR provider question resolved).

**Implement:** `ImportExpenseFromDocument`/`ImportRevenueFromDocument` (calls the Document Extraction adapter, creates a draft record with `import_source = pdf_ocr`) and `ConfirmImportedExpense`/`ConfirmImportedRevenue` from `contracts.md`. A draft never counts toward reports until confirmed — enforce this as a hard invariant (e.g. a query flag or separate draft state), not just a UI convention.

**Automated tests:** unit test proving a draft is excluded from any aggregate/report-facing query until confirmed; integration test for the full upload → draft → confirm → `bookkeeping.*-recorded.v1` path, using a fixture PDF and a faked/stubbed OCR adapter so the test doesn't depend on a live third-party service.

**What you can test:** upload a sample receipt/invoice PDF (ask the agent for a fixture), see the draft it produces, correct any fields the OCR got wrong, confirm it, and see it now appear as a normal expense/revenue.

### Task 4.5 — Plan CRUD + planned lines + plan-vs-actual

**Depends on:** 4.3.

**Implement:** `CreatePlan`/`UpdatePlan`/`ArchivePlan`, `AddPlannedExpenseLine`/`AddPlannedRevenueLine`, `ListPlans`/`GetPlanById`, `GetPlanVsActual` from `contracts.md`. Publishes `bookkeeping.plan-created.v1` / `bookkeeping.planned-expense-added.v1` / `bookkeeping.planned-revenue-added.v1` per `events.md`.

**Automated tests:** unit test for the plan-vs-actual comparison logic; integration tests for CRUD + outbox.

**What you can test:** create a plan with an allocation and a period, add a planned expense line and a planned revenue line, then check plan-vs-actual reflects the real expense/revenue you recorded in Task 4.3 against the same categories/period.

### Task 4.6 — Experience API + frontend: Expenses, Revenues, Plans screens

**Depends on:** 4.3, 4.4, 4.5, 1.8.

**Implement:** Experience API endpoints for the Expenses, Revenues, and Plans screens, per `screens-and-features.md`. Frontend slices in `features/expenses/`, `features/revenues/`, `features/plans/`, including the PDF upload UI and the draft-review/confirm step.

**Automated tests:** component tests for CRUD forms, the reconciliation picker, and the draft-confirmation review UI; Experience API integration tests per endpoint.

**What you can test:** in the browser, record an expense and reconcile it against a transaction; upload a sample receipt PDF, review and confirm the resulting draft; create a plan with a planned expense line and see it reflected in plan-vs-actual on the Plans screen.

### Task 4.7 — Phase 4 Playwright E2E

**Depends on:** 4.6.

**Implement:** the Phase 4 Playwright test from `workplan.md`: record an expense manually and reconcile it against a Phase 2 transaction; upload a sample receipt PDF and confirm the resulting draft; create a plan with a planned expense line and see it reflected in plan-vs-actual.

**Automated tests:** the Playwright spec itself.

**What you can test:** run the Playwright suite and watch this flow execute; repeat it once by hand in the browser.

---

## Phase 5 — Reporting & Dashboards

### Task 5.1 — Confirm the spec slice

**Depends on:** 3.8, 4.7 (needs both parallel phases done — this is the join point in the dependency graph).

**Implement:** no code. Re-read `services.md`'s Reporting section, `contracts.md`'s Reporting section, and the full `events.md` catalog — this is the first phase where every previously defined event actually gets consumed, so confirm the projection shapes needed for `GetFinancialOverview` and the Reports screen are concrete enough to build.

**Automated tests:** none.

**What you can test:** review the agent's confirmation or raised gap.

### Task 5.2 — Reporting service: schema + full projection inbox

**Depends on:** 5.1.

**Implement:** create `src/Contapop.Reporting.Service/` with read-model projection tables in `contapop_reporting` (per `services.md`, populated only from events — never queries other services' databases directly). Subscribe to every integration event published so far: `identity.project-*.v1`, `ledger.transaction-*.v1`, `billing.invoice-issued.v1`/`-paid.v1`/`-overdue.v1`/`-archived.v1`/`payment-recorded.v1`, `bookkeeping.expense-recorded.v1`/`revenue-recorded.v1`/`plan-created.v1`/`planned-expense-added.v1`/`planned-revenue-added.v1`. Project each into the read models `GetFinancialOverview` needs (totals, trends by period); an archived invoice must no longer contribute to invoice totals.

**Automated tests:** a projection-correctness integration test per event type — publish a fabricated event, assert the projection updates correctly; a re-delivery test proving idempotency across all of them.

**What you can test:** with real data already created across Phases 2–4 (your test bank account, transactions, invoices, expenses, revenues, plans), query the Reporting service's projection tables directly and confirm the numbers match what you'd expect by hand-adding the source records.

### Task 5.3 — `GetFinancialOverview` + Report CRUD

**Depends on:** 5.2.

**Implement:** `GetFinancialOverview` and `SaveReport`/`UpdateReport`/`DeleteReport`/`ListReports`/`GetReportById` from `contracts.md`. A saved report stores its title/criteria only; content is always computed fresh from the Task 5.2 projections at read time, per `contracts.md`'s note that Report content is never frozen at save time.

**Automated tests:** unit tests for the overview aggregation logic; integration tests for report save/reopen returning currently-fresh figures rather than stale ones.

**What you can test:** call `GetFinancialOverview` directly and cross-check the numbers against your own manual tally of what you've entered so far; save a report with some criteria, then add one more expense afterward and reopen the saved report — confirm it reflects the new figure rather than what was true when you saved it.

### Task 5.4 — Experience API + frontend: real Home widgets, Financial Overview charts, Reports screen

**Depends on:** 5.3, 1.9 (replaces the Phase 1 greeting-only Home shell).

**Implement:** replace Phase 1's Home greeting-only shell with its real widgets (financial metrics, recent activity, quick nav — per `screens-and-features.md`); build the Financial Overview screen's charts/summaries; build the Reports screen (generate, save, edit, delete, reopen).

**Automated tests:** component tests for chart rendering given known projection data (mocked); Experience API integration tests for the composed home/financial-overview/reports endpoints.

**What you can test:** in the browser, see Home now show real totals instead of just a greeting; see Financial Overview's charts reflect the data you've entered across every earlier phase; generate a report, save it, reopen it from the list, and confirm the figures are consistent with what you see elsewhere in the app.

### Task 5.5 — Phase 5 Playwright E2E

**Depends on:** 5.4.

**Implement:** the Phase 5 Playwright test from `workplan.md`: using data created across Phases 2–4, confirm Home and Financial Overview show correct aggregated totals; generate a report, save it, reopen it from the Reports list, confirm the figures are consistent.

**Automated tests:** the Playwright spec itself.

**What you can test:** run the Playwright suite and watch this flow execute; repeat it once by hand in the browser.

---

## Phase 6 — Hardening & Launch Readiness

Phase 6 is less uniformly "vertical slice, then Playwright" than Phases 1–5, since its deliverables are cross-cutting (hosting, notifications, security, regression) rather than one more product surface. Two of its tasks (6.1, 6.4) surface decisions that are explicitly still open in `scope-decisions.md` and must be resolved with Gerardo before the dependent tasks can start — they are not something an agent should decide alone.

### Task 6.1 — Resolve hosting target

**Depends on:** nothing structurally (can happen any time after Phase 1, but gates 6.2/6.3/6.5).

**Implement:** no code. This is `scope-decisions.md`'s still-open "hosting target" item (Azure Container Apps vs. AKS vs. other) — surface the trade-offs and get a decision from Gerardo, then record it in `scope-decisions.md`.

**Automated tests:** none.

**What you can test:** review the options the agent presents and make the call; confirm `scope-decisions.md` is updated with the decision afterward.

### Task 6.2 — Deployment pipeline

**Depends on:** 6.1, 1.10 (extends the Phase 1 CI skeleton).

**Implement:** a real CD pipeline targeting the chosen hosting platform, deploying all six services + frontend from the CI skeleton built in Task 1.10.

**Automated tests:** a pipeline dry-run/staging deploy.

**What you can test:** watch a deploy actually run against the chosen environment and confirm the app is reachable there afterward.

### Task 6.3 — In-app notifications for overdue/upcoming reminders

**Depends on:** 6.2 (needs a real environment to matter, though it can be built and tested locally first).

**Implement:** the "reminders for overdue/upcoming" language scattered across the MVP screens (`screens-and-features.md`) — in-app at minimum, wired to the overdue-invoice event from Phase 3 and any upcoming-expense/plan logic from Phase 4. Email is a stretch goal per `workplan.md`, not required for this task to be done.

**Automated tests:** unit tests for the notification-triggering conditions; component tests for the notification UI.

**What you can test:** trigger an overdue invoice (as in Task 3.6) and confirm an in-app notification appears; if email was included, check a real inbox.

### Task 6.4 — GDPR/security pass

**Depends on:** 6.2.

**Implement:** a security and data-protection review sized for a private pilot (not a public launch) — per `scope-decisions.md`. Likely outcomes: confirming secrets aren't logged (per `backend-api-code-guidelines.md`'s Security section), a basic data-export/delete-my-data path if Gerardo decides pilot users need one, and a review of the internal JWT signing-key handling flagged as an open item in `services.md`. This task's exact scope depends on a short discussion with Gerardo before starting — treat the review's findings as a checklist to confirm with him, not something to silently resolve.

**Automated tests:** targeted tests for whatever concrete findings come out of the review (e.g. a log-scrubbing test if that's a finding).

**What you can test:** review the findings list with the agent and decide together what's in scope for the pilot vs. deferred.

### Task 6.5 — Full critical-path regression against the real deployment

**Depends on:** 6.2, 6.3, and every phase's Playwright suite (1.10, 2.7, 3.8, 4.7, 5.5).

**Implement:** run the full critical-path Playwright suite from every prior phase against the real deployed pilot environment (not local Aspire), per `workplan.md`'s Phase 6 E2E test: link account → issue invoice → record expense → reconcile → view report, end to end, on the real deployment.

**Automated tests:** the assembled critical-path suite itself, pointed at the deployed environment's URL.

**What you can test:** watch the suite run against the real pilot URL, then repeat the same chain yourself by hand in a browser against that same URL — this is the MVP's final sign-off before inviting real pilot users in.

---

## Task Count Summary

| Phase | Tasks | Notable join/fork points |
|---|---|---|
| 1. Foundation & Identity/Tenancy | 1.1–1.10 (10) | — |
| 2. Financial Accounts & Ledger | 2.1–2.7, 2.5a (8) | First inbox/replica proof (2.2); global reconciliation claims (2.5a) |
| 3. Billing & Invoicing | 3.1–3.8, 3.7a–3.7k (19) | Can run parallel to Phase 4 from 3.1 onward |
| 4. Bookkeeping & Planning | 4.1–4.7 (7) | Can run parallel to Phase 3 from 4.1 onward; 4.1/4.4 carry the open OCR-provider question |
| 5. Reporting & Dashboards | 5.1–5.5 (5) | 5.1 depends on both 3.8 and 4.7 — the fork rejoins here |
| 6. Hardening & Launch Readiness | 6.1–6.5 (5) | 6.1 and 6.4 need Gerardo's input, not just agent execution |

**54 tasks total.** If Phases 3 and 4 genuinely run as two parallel agent workstreams once Phase 2 (including 2.5a) is done, the critical path through the whole MVP is 1 → 2 → (3 or 4, whichever finishes later) → 5 → 6, i.e. roughly 10 + 8 + 19 + 5 + 5 = 47 tasks deep rather than 54 if fully serialized.
