# MVP Scope Decisions

This document records scope cut-lines for the MVP — decisions about how much of the domain model documented in `docs/analysis/domain.md` is actually exposed and enforced in v1, as opposed to permanent domain facts. Where a decision changed an entity's shape (not just its usage), that change is also reflected directly in `domain.md`.

## Currency: EUR only

**Decision (2026-09-06):** the MVP supports EUR only. No `currency` field is added to any money-bearing entity (Expense, Revenue, Invoice/Ticket, Payment, Transaction, Plan, Planned Revenue, Planned Expense). All `amount`-shaped fields are implicitly EUR, stored as integer minor units per `docs/standards/tech-stack.md`.

**Why:** simplest schema for every service that touches money, and matches a Spain-based freelancer/small-business target market for launch. Because amounts are already integer minor units, adding a `currency` column later is an additive migration, not a breaking one — multi-currency isn't foreclosed, just deferred.

## Invoice Tax Handling: Basic VAT/IVA Breakdown

**Decision (2026-09-06):** Invoice carries a `net_amount` / `tax_rate` / `tax_amount` / `total_amount` breakdown (applied in `domain.md`) rather than a single `amount` field. Full regulatory compliance — Facturae electronic invoice format, SII (Suministro Inmediato de Información) reporting to the Agencia Tributaria, and the Verifactu anti-fraud software requirements — is explicitly **out of scope for MVP**.

**Why:** a bare total amount isn't usable for real Spanish accounting even informally, so the minimum breakdown is worth the small extra cost now. Full compliance is a materially larger scope (certified software requirements, specific XML/electronic formats, real-time tax authority reporting) that deserves its own dedicated phase rather than folding into MVP.

**Follow-up needed:** confirm the current Verifactu applicability timeline before committing to a post-MVP compliance phase's timing — Spanish regulation in this area has been in flux and the exact deadlines should be re-verified closer to that phase rather than assumed from what's known today.

## Invoice Removal

**Decision (2026-09-12):** a payment-free `draft` Invoice may be permanently deleted as an accidental, pre-financial-workflow entry. `issued`, `overdue`, and `void` payment-free invoices use `ArchiveInvoice` instead. `paid` invoices and any invoice with a Payment record cannot be deleted or archived.

**Why:** an accidental draft has never become a reporting fact, so retaining it as financial history has no value. Once an Invoice has entered the financial workflow, archival preserves the owned record and produces the existing Reporting removal event.

**Consequence:** `DeleteDraftInvoice` removes the Invoice, all Invoice Lines, and attachment metadata transactionally. Its blob is queued for durable retry cleanup; no draft-deletion integration event is needed because Reporting receives Invoice data only from `billing.invoice-issued.v1` onward.

## Draft Invoice Lines and Editing

**Decision (2026-09-12):** MVP invoice creation supports one or more itemized lines. Only a draft can be edited: counterparty, direction, and type remain immutable after creation, while invoice date, due date, and the complete line collection are mutable. An edit replaces all persisted lines atomically; it does not patch individual lines.

**Why:** retaining those three identity fields prevents a draft from silently becoming a different commercial document after creation, while complete line replacement keeps the editing model small and makes the server the single source of truth for VAT and aggregate recalculation. An incorrectly selected immutable value is corrected by deleting the payment-free draft and creating a new one.

**Consequence:** each submitted line must have a description, positive integer quantity, positive EUR minor-unit price, and VAT rate. The service recomputes each line's net, banker's-rounded VAT, and total, then the Invoice aggregates; client-side calculations are previews only. No integration event is emitted for creation or editing of a draft because Reporting has no draft projection.

## Project Scope: Single Implicit Project

**Decision (2026-09-06):** every tenant gets exactly one Project, auto-created behind the scenes when the tenant is provisioned. There is no Projects management screen, no project switcher, and no user-facing CRUD for Project in the MVP. This matches the fact that none of the 11 MVP screens in `screens-and-features.md` mention Projects at all.

**Why:** the domain model, the Project sync events (`identity.project-*.v1` in `docs/analysis/events.md`), and the cross-service consistency mechanism in `docs/analysis/services.md` all stay exactly as designed — every other entity still carries `project_id` and validates it the same way. This decision only removes UI and API surface for managing multiple projects; it doesn't change any entity shape or event.

**Impact on services:** Identity & Tenancy's MVP build is smaller than its full design — it needs to auto-provision one Project per Tenant on signup, but not full Project CRUD endpoints or a management UI. Full multi-project support (a real Projects screen, a project switcher across the app) is a fast-follow, not a rebuild, since nothing else needs to change to support it later.

## Multi-User Scope: Single-Owner Only

**Decision (2026-09-06):** the MVP is single-owner only — one User per Tenant, the account owner. No invite flow, no collaborator role, no permissions UI.

**Why:** matches `docs/standards/tech-stack.md`, which already describes "account-owner and future collaborator access" — collaboration was already anticipated as future work, not MVP scope, before this decision made it explicit.

**Impact on services:** Identity & Tenancy's MVP build needs authentication and a single owner role, but not an invite flow, role management, or a permissions matrix beyond "the owner can do everything." The `User` entity's shape in `domain.md` doesn't change — a tenant could still technically have multiple User rows — this decision only means the MVP product doesn't expose a way to create a second one.

## Transaction Reconciliation

**Decision (2026-09-06, clarified 2026-09-08):** full reconciliation is in MVP scope. A Payment, Expense, or Revenue can each optionally reference the Financial Accounts & Ledger Transaction it was matched against, via `reconciled_transaction_id` (applied to `domain.md`). One-to-one globally for MVP — one Transaction can reconcile with at most one Payment, Expense, or Revenue, and vice versa. No splitting one transaction across multiple records or vice versa.

**Why:** this is more work than the "no linking" alternative, since it makes Transaction a second reference-data hub (alongside Identity & Tenancy's Project) that Bookkeeping & Planning and Billing & Invoicing both need to replicate locally and validate against — see `docs/analysis/events.md`'s `ledger.transaction-recorded.v1`/`ledger.transaction-updated.v1`/`ledger.transaction-archived.v1` domain synchronization events and `docs/analysis/services.md`'s updated Cross-Service Data Consistency Strategy. Chosen anyway because it directly supports a core freelancer workflow: turning bank statement lines into bookkeeping entries and confirming an invoice was actually paid.

**Consequence:** Transaction is now soft-deleted (`status: active/archived`) instead of hard-deleted, the same pattern as Project, since a reconciled reference must never dangle. Financial Accounts & Ledger owns durable Transaction Reconciliation Claims, which reserve, confirm, and release the one permitted cross-service reconciliation; the Experience API coordinates this narrow compensation protocol. See `services.md`'s **Strict global Transaction reconciliation** section.

## PDF Invoice/Receipt Import

**Decision (2026-09-06):** full OCR extraction is in MVP scope, reversing the earlier "later phase" framing in `docs/analysis/api-led/system-apis.md`. The Document Extraction System API is implemented as an Infrastructure-layer adapter inside Bookkeeping & Planning (not a standalone deployable, since it has one consuming service for now).

**Why:** `screens-and-features.md` already listed this as a key feature of the Expenses and Revenues screens; bringing the underlying integration forward avoids shipping a screen feature the backend doesn't support.

**Consequence:** `Expense` and `Revenue` gained an `import_source` field (`manual` or `pdf_ocr`). An OCR-imported record is created as a draft that a user must explicitly confirm (`ConfirmImportedExpense`/`ConfirmImportedRevenue` in `docs/analysis/contracts.md`) before it counts toward any report — OCR accuracy isn't assumed to be perfect, so nothing silently enters the books unreviewed.

## MVP Audience & Onboarding

**Decision (2026-09-06):** the MVP is a small private pilot — a handful of real users, manually onboarded (Gerardo provisions each tenant and its single user directly). No public self-serve signup flow, email-verification-driven registration UX, or marketing/landing surface is needed for v1.

**Why:** combined with the single-owner and single-project decisions above, this further narrows Identity & Tenancy's actual MVP build: real authentication is required, but registration polish, self-serve tenant creation, and onboarding UX are not. Moving to a public launch later needs a real signup flow added — it doesn't require redesigning what's already built.

## Test Rigor

**Decision (2026-09-06):** critical paths only. Playwright end-to-end coverage for the money-critical flows (issue an invoice, record an expense, link a bank account, and similarly consequential actions), plus unit tests on domain logic (aggregates, invariants). Integration tests on lower-stakes CRUD screens are not required for v1.

## Delivery Model

Gerardo guides development directly; most implementation is carried out by AI coding agents rather than a traditional human development team. This changes what actually limits how fast the six services can move: it isn't developer head-count, it's how precisely each service's contract (commands, queries, OpenAPI shape) is specified before implementation starts — agents parallelize cleanly against an unambiguous spec and poorly against one that still has open questions. This raises the priority of finishing the command/query and OpenAPI contract catalog per service (see Open Items in `docs/analysis/services.md`-adjacent docs) relative to what a conventional human-team project would need before starting to build.

**Hosting target:** not yet decided (Azure Container Apps vs. AKS vs. other). Doesn't block domain, contract, or local-Aspire-development work, but needs an answer before any CI/CD or infrastructure-as-code tickets are written.

## Summary

| Area | MVP decision | Entity shape changed? |
|---|---|---|
| Currency | EUR only | No — no currency field added |
| Invoice tax | Basic VAT/IVA breakdown; full compliance deferred | Yes — `domain.md`'s Invoice entity |
| Invoice removal | Permanently delete payment-free drafts; archive payment-free issued, overdue, and void invoices | No — lifecycle rule only |
| Project | Single implicit project per tenant, no management UI | No — usage/UI scope only |
| Multi-user | Single-owner only, no invite flow | No — usage/UI scope only |
| Audience/onboarding | Small private pilot, manually onboarded | No — usage/UI scope only |
| Test rigor | Critical paths only (E2E + unit) | N/A |
| Hosting target | Not yet decided | N/A |
| Transaction reconciliation | Full reconciliation (globally one-to-one), in MVP | Yes — `domain.md`'s Payment/Expense/Revenue/Transaction Reconciliation Claim |
| PDF invoice/receipt import | Full OCR extraction, in MVP | Yes — `domain.md`'s Expense/Revenue (`import_source`) |
