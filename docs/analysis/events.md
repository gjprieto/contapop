# Events

This document catalogs the events flowing through Contapop, following the event conventions in `docs/standards/architecture-guidelines.md` (Domain Events, Outbox, Inbox, and Event-Driven Integration sections). It starts with the domain synchronization events that back the Cross-Service Data Consistency Strategy in `docs/analysis/services.md`, and sketches the wider event catalog as a first pass to be refined service by service.

## Event Categories

Two distinct kinds of event appear in this system, and only one of them is documented here in full:

- **Domain events** are internal to a bounded context — raised on an aggregate during command handling, immutable, past tense (e.g. `InvoiceIssued`). Per the architecture guidelines, a domain event is never itself a public contract; it's translated at the application/infrastructure boundary before anything crosses a service boundary. This document doesn't catalog these exhaustively — they're an internal implementation detail of each service — but the last section lists representative examples so the terminology is unambiguous.
- **Integration events** are the versioned, public contracts published through a service's transactional outbox and consumed by other services through their inbox. Every event in the main catalog below is an integration event. This document's job is to make these a shared, explicit contract between services.

A specific subset of integration events — the ones whose only job is keeping a consuming service's local read replica in sync, per the Cross-Service Data Consistency Strategy — are called **domain synchronization events** here. They're listed first because that's this document's starting point.

## Naming & Envelope Conventions

**Name:** `{bounded-context}.{event-name}.v{major}` (matches `architecture-guidelines.md`, e.g. `invoicing.invoice-issued.v1`). Bounded-context slugs match the database names already chosen in `services.md`: `identity`, `ledger` (Financial Accounts & Ledger), `billing` (Billing & Invoicing), `bookkeeping` (Bookkeeping & Planning). Reporting and the Experience API don't publish integration events — they only consume.

**Envelope** — every integration event carries these fields regardless of payload, per the outbox record shape already specified in the guidelines:

| Field | Purpose |
|---|---|
| `event_id` | Unique message ID (dedup key on the consumer's inbox) |
| `event_name` | e.g. `identity.project-created.v1` |
| `aggregate_type` / `aggregate_id` | What changed |
| `aggregate_version` | Lets a consumer ignore an out-of-order or already-applied event |
| `tenant_id` | Every event is tenant-scoped |
| `occurred_at` | UTC timestamp of the fact, not the publish time |
| `correlation_id` / `causation_id` | Traceability across the command → event → reaction chain |
| `payload` | Event-specific fields, listed per event below |

**Delivery:** at-least-once, per the guidelines. Every consumer here applies the standard inbox pattern — reject if `event_id` already processed for that consumer, apply only if `aggregate_version` is newer than the replica's last-applied version, tolerate reordering across different aggregates.

**Topics:** one Dapr pub/sub topic per bounded context: `identity.events`, `ledger.events`, `billing.events`, and `bookkeeping.events`. Producers publish every integration event for their bounded context to its topic; consumers subscribe to that topic and select the event types they handle from the envelope's `event_name`. This keeps topic count low at the current service count while preserving explicit, versioned event contracts.

## How the Candidate Entities Were Selected

Each of `domain.md`'s 15 entities was checked against the six declared services in `services.md` for foreign keys that cross a service boundary — the same test already applied to Project. The result:

| Entity | Owning service | Foreign keys | Crosses a service boundary? |
|---|---|---|---|
| Tenant | Identity & Tenancy | — | n/a (top of the hierarchy) |
| User | Identity & Tenancy | `tenant_id` | No — internal to Identity & Tenancy |
| Project | Identity & Tenancy | `tenant_id` | No — internal to Identity & Tenancy |
| Bank Account | Financial Accounts & Ledger | `tenant_id`, `project_id` | **Yes — `project_id`** |
| Credit/Debit Card | Financial Accounts & Ledger | `tenant_id`, `project_id` | **Yes — `project_id`** |
| Transaction | Financial Accounts & Ledger | `bank_account_id` | No, but **referenced FROM other services** — see below |
| Invoice/Ticket | Billing & Invoicing | `tenant_id`, `project_id`, `counterparty_id` | **Yes — `project_id`** (`counterparty_id` is internal to Billing & Invoicing) |
| Counterparty | Billing & Invoicing | `tenant_id` | No — internal to Billing & Invoicing |
| Payment | Billing & Invoicing | `invoice_or_ticket_id`, `reconciled_transaction_id` | **Yes — `reconciled_transaction_id`** (2026-09-06 reconciliation decision) |
| Expense | Bookkeeping & Planning | `tenant_id`, `project_id`, `reconciled_transaction_id` | **Yes — `project_id` and `reconciled_transaction_id`** |
| Revenue | Bookkeeping & Planning | `tenant_id`, `project_id`, `reconciled_transaction_id` | **Yes — `project_id` and `reconciled_transaction_id`** |
| Plan (absorbs former Budget) | Bookkeeping & Planning | `tenant_id`, `project_id` | **Yes — `project_id`** |
| Planned Revenue | Bookkeeping & Planning | `tenant_id`, `project_id`, `plan_id` | **Yes — `project_id`** (`plan_id` is internal) |
| Planned Expense | Bookkeeping & Planning | `tenant_id`, `project_id`, `plan_id` | **Yes — `project_id`** (`plan_id` is internal) |
| Report | Reporting | `tenant_id`, `project_id` | **Yes — `project_id`** |

**Result, updated 2026-09-06: Project and Transaction are the entities that need domain synchronization events.** Transaction wasn't referenced cross-service when this table was first built — the 2026-09-06 decision to support full reconciliation (an Expense, Revenue, or Payment can point at the Transaction it was matched against) made Financial Accounts & Ledger a second reference-data hub alongside Identity & Tenancy. Every other cross-entity reference stays inside the owning service (Payment→Invoice, Invoice→Counterparty, PlannedRevenue/PlannedExpense→Plan) or is `tenant_id`, which is excluded for the reason below.

**Entities evaluated and excluded:**

- **Tenant** — appears as `tenant_id` on every entity in the system, but it's resolved from the authenticated request's tenant context (token/claim), not looked up as a business reference the way `project_id` is. No service needs a local Tenant replica to validate a write; tenant scoping and any suspension/lockout enforcement belong at the auth/gateway layer, not this data-consistency mechanism. This resolves the `identity.tenant-status-changed.v1` candidate raised previously — it's out of scope here, not merely deferred.
- **User** — no entity in `domain.md` carries a `user_id` foreign key (only `tenant_id` and `project_id` appear outside a service's own aggregates), so no other service ever needs to validate a User reference. This resolves the `identity.user-created.v1` candidate raised previously — not needed under the current domain model.
- **Counterparty (Customer/Supplier)** — added to `domain.md` (2026-09-06), living inside Billing & Invoicing alongside Invoice and Payment (see `services.md`). Invoice's reference to it (`counterparty_id`) is an internal foreign key, not cross-service, so no synchronization event is needed. It would only need one if another service (e.g. Bookkeeping & Planning, for a supplier-tagged Expense) started referencing it directly — `domain.md` doesn't model that today; revisit if that gap gets filled.

## Domain Synchronization Events

These are the events that make the Cross-Service Data Consistency Strategy work. There are now two reference-data hubs:

- **Identity & Tenancy**, publishing Project lifecycle events, consumed by Financial Accounts & Ledger, Billing & Invoicing, Bookkeeping & Planning (to validate `project_id`), and Reporting (for display).
- **Financial Accounts & Ledger**, publishing Transaction lifecycle events (new, 2026-09-06), consumed by Bookkeeping & Planning and Billing & Invoicing (to validate `reconciled_transaction_id`), and Reporting (for display).

### `identity.project-created.v1`

**Producer:** Identity & Tenancy, on Project creation.
**Consumers:** Financial Accounts & Ledger, Billing & Invoicing, Bookkeeping & Planning (insert a new row into their local Project read-replica); Reporting (projection).

| Payload field | Notes |
|---|---|
| `project_id` | |
| `tenant_id` | |
| `name` | |
| `status` | Always `active` on creation |
| `created_at` | |

### `identity.project-renamed.v1`

**Producer:** Identity & Tenancy, when a Project's name changes.
**Consumers:** same four services — updates the `name` field on the local replica row if `aggregate_version` is newer than what's stored.

| Payload field | Notes |
|---|---|
| `project_id` | |
| `tenant_id` | |
| `name` | The new name |
| `renamed_at` | |

### `identity.project-archived.v1`

**Producer:** Identity & Tenancy, when a Project is archived. Per the soft-delete/tombstone approach already agreed for referential integrity, this never becomes a hard delete — the Project ID is never reused or removed.
**Consumers:** same four services — sets `status = archived` on the local replica row. Per the deferred compensation strategy, this is also the trigger a future reconciliation/saga mechanism would react to when deciding whether to flag existing dependents (Bank Accounts, Invoices, Expenses already pointing at this Project).

| Payload field | Notes |
|---|---|
| `project_id` | |
| `tenant_id` | |
| `archived_at` | |

### `identity.project-reactivated.v1`

**Producer:** Identity & Tenancy, if an archived Project is reactivated.
**Consumers:** same four services — sets `status = active` again.

| Payload field | Notes |
|---|---|
| `project_id` | |
| `tenant_id` | |
| `reactivated_at` | |

### `ledger.transaction-recorded.v1`

**Producer:** Financial Accounts & Ledger, on `RecordTransaction` or as part of `ImportTransactionsFromFile` (see `docs/analysis/contracts.md`).
**Consumers:** Bookkeeping & Planning and Billing & Invoicing (insert into their local Transaction read-replica so `reconciled_transaction_id` can be validated); Reporting (projection). This event already existed in the "Other Anticipated Integration Events" list below as a business fact for Reporting — as of 2026-09-06 it does double duty as a domain synchronization event too. Later changes are propagated by `ledger.transaction-updated.v1`.

| Payload field | Notes |
|---|---|
| `transaction_id` | |
| `tenant_id` | |
| `bank_account_id` | |
| `amount` | |
| `date` | |
| `type` | |
| `description` | Optional statement or user-provided transaction description |
| `status` | Always `active` on creation |
| `created_at` | |

### `ledger.transaction-archived.v1`

**Producer:** Financial Accounts & Ledger, when a Transaction is deleted from the Transactions screen. Per the same soft-delete/tombstone reasoning as Project, this is never a hard delete once Transaction can be referenced cross-service — the ID is never reused or removed.
**Consumers:** Bookkeeping & Planning and Billing & Invoicing — sets `status = archived` on the local replica row. This is the trigger the deferred compensation mechanism would react to for any Expense/Revenue/Payment still pointing at this Transaction, same as `identity.project-archived.v1` is for Project.

| Payload field | Notes |
|---|---|
| `transaction_id` | |
| `tenant_id` | |
| `archived_at` | |

### `ledger.transaction-updated.v1`

**Producer:** Financial Accounts & Ledger, when `UpdateTransaction` succeeds.
**Consumers:** Bookkeeping & Planning and Billing & Invoicing update their local Transaction read-replica when `aggregate_version` is newer; Reporting updates its transaction projection.

| Payload field | Notes |
|---|---|
| `transaction_id` | |
| `tenant_id` | |
| `bank_account_id` | |
| `amount` | |
| `date` | |
| `type` | |
| `description` | Optional statement or user-provided transaction description |
| `status` | Always `active`; archived transactions cannot be updated |
| `updated_at` | |

## Other Anticipated Integration Events (first pass)

These aren't domain synchronization events — nothing replicates them into a local read-replica for write-time validation — but they're the business-fact events Reporting's projections will need. Their payloads are specified when their producing service is ready to implement them: Identity and Ledger are already specified, Billing is specified by Task 3.1, and Bookkeeping & Planning remains deferred to Task 4.1. Billing publishes no event when it permanently deletes a draft Invoice: drafts are not Reporting facts and never have a Reporting projection to remove.

### `identity.tenant-created.v1`

**Producer:** Identity & Tenancy, on tenant provisioning (`ProvisionTenant`, see `docs/analysis/contracts.md`).
**Aggregate:** `Tenant` / `tenant_id` — a record of the fact for audit/reporting purposes, not a domain synchronization event; Tenant is still excluded from that category per the reasoning above (no other service holds a local Tenant replica).
**Consumers:** Reporting only, for its audit/admin projections. No other service consumes this in Phase 1 — Financial Accounts & Ledger, Billing & Invoicing, and Bookkeeping & Planning key everything off `project_id`, not `tenant_id`, so they have no replica row to create from it.

| Payload field | Notes |
|---|---|
| `tenant_id` | |
| `name` | The tenant/business name (`tenantName` on `ProvisionTenant`) |
| `owner_user_id` | The initial owner User created alongside the tenant |
| `created_at` | |

### `billing.invoice-issued.v1`

**Producer:** Billing & Invoicing, when `IssueInvoice` succeeds.
**Consumers:** Reporting.

| Payload field | Notes |
|---|---|
| `invoice_id` | |
| `project_id` | |
| `counterparty_id` | |
| `direction` | `incoming` or `outgoing` |
| `type` | `service` or `product` |
| `net_amount_minor` | EUR minor units |
| `tax_amount_minor` | EUR minor units |
| `total_amount_minor` | EUR minor units |
| `date` | Invoice business date |
| `due_date` | |
| `issued_at` | |

### `billing.payment-recorded.v1`

**Producer:** Billing & Invoicing, when `RecordPayment` succeeds.
**Consumers:** Reporting.

| Payload field | Notes |
|---|---|
| `payment_id` | |
| `invoice_id` | |
| `project_id` | Copied from the associated invoice for projection convenience |
| `amount_minor` | EUR minor units |
| `date` | Payment business date |
| `payment_method` | |
| `recorded_at` | |

### `billing.invoice-paid.v1`

**Producer:** Billing & Invoicing, when accepted payments for an invoice first reach its `total_amount_minor` and the invoice transitions to `paid`.
**Consumers:** Reporting.

| Payload field | Notes |
|---|---|
| `invoice_id` | |
| `project_id` | |
| `direction` | `incoming` or `outgoing` |
| `total_amount_minor` | Invoice amount now fully paid, in EUR minor units |
| `paid_at` | |

### `billing.invoice-overdue.v1`

**Producer:** Billing & Invoicing, when `MarkInvoicesOverdue` transitions an issued invoice to `overdue`.
**Consumers:** Reporting; Phase 6 notifications.

| Payload field | Notes |
|---|---|
| `invoice_id` | |
| `project_id` | |
| `direction` | `incoming` or `outgoing` |
| `total_amount_minor` | EUR minor units |
| `due_date` | |
| `overdue_at` | |

### `billing.invoice-archived.v1`

**Producer:** Billing & Invoicing, when `ArchiveInvoice` succeeds for a payment-free `issued`, `overdue`, or `void` invoice. A permanently deleted draft does not publish this event because Reporting never receives a draft projection.
**Consumers:** Reporting; Phase 6 notifications remove any pending reminder for the invoice.

| Payload field | Notes |
|---|---|
| `invoice_id` | |
| `project_id` | |
| `direction` | `incoming` or `outgoing` |
| `previous_status` | `issued`, `overdue`, or `void` |
| `archived_at` | |

- **Financial Accounts & Ledger:**

### `ledger.bank-account-linked.v1`

**Producer:** Financial Accounts & Ledger, when `LinkBankAccount` succeeds.
**Consumers:** Reporting only, when its account-level projections are introduced in Phase 5.

| Payload field | Notes |
|---|---|
| `bank_account_id` | |
| `tenant_id` | |
| `project_id` | |
| `bank_name` | |
| `status` | Always `active` on creation |
| `created_at` | |

`account_number` is deliberately excluded. It is sensitive financial data owned by Financial Accounts & Ledger, and no current downstream consumer needs it.

### `ledger.card-linked.v1`

**Producer:** Financial Accounts & Ledger, when `AddPaymentCardLabel` succeeds.
**Consumers:** Reporting only, when its card-level projections are introduced in Phase 5.

| Payload field | Notes |
|---|---|
| `card_id` | |
| `tenant_id` | |
| `project_id` | |
| `label` | Metadata only; never a real card number |
| `created_at` | |

- **Bookkeeping & Planning:** `bookkeeping.expense-recorded.v1`, `bookkeeping.revenue-recorded.v1`, `bookkeeping.plan-created.v1`, `bookkeeping.planned-expense-added.v1`, `bookkeeping.planned-revenue-added.v1`

## Domain Events (internal, not public contracts — examples only)

Listed for terminology only; these live inside a single service and are never subscribed to directly by another service. A domain event is mapped to one of the integration events above (if any external service needs to know) at the outbox boundary — the two are not the same object.

Examples: `InvoiceIssued`, `InvoiceMarkedPaid`, `ExpenseRecorded`, `BankAccountLinked`, `TransactionCategorized`, `PlanRevised`.

## Open Items

1. The Bookkeeping & Planning anticipated integration events remain a placeholder outline and must receive full payload contracts during that service's Task 4.1 spec checkpoint. Identity, Ledger, and Billing events needed by their implemented/planned phases are fully specified above.
2. If Bookkeeping & Planning ever references Counterparty directly (e.g. a supplier-tagged Expense), that would introduce a new cross-service candidate and a matching set of `billing.counterparty-*` synchronization events — not needed under the current domain model.
3. Reconciliation (`reconciled_transaction_id`) is modeled as optional and globally one-to-one for MVP — a Transaction reconciles to at most one Expense, Revenue, or Payment, and vice versa. Ledger-owned Transaction Reconciliation Claims enforce the cross-service side of this invariant; see `services.md`. Split transactions or many-to-one reconciliation are not supported by this shape; revisit if that turns out to be needed.
4. `Expense`/`Revenue`'s new `import_source` (`manual` vs `pdf_ocr`) and the "draft pending confirmation" step for OCR-imported records (see `contracts.md`) don't currently raise a distinct event from `bookkeeping.expense-recorded.v1`/`bookkeeping.revenue-recorded.v1` — confirmation only happens once, after which it's an ordinary record. Revisit if Reporting or anything else needs to distinguish OCR-sourced records specifically.
