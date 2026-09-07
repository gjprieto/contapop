# Contapop — Agent Implementation Guide

This file is the main reference for any AI coding agent (Claude, opencode, or otherwise) implementing Contapop. It is deliberately tool-agnostic. Read it before writing any code, and re-read the relevant spec section before starting each task.

Contapop is a Spanish accountancy SaaS for freelancers/small businesses. Gerardo (Software Architect, Alicazum SL) guides the work; most implementation is expected to come from AI coding agents. Because of that, **the specs in `docs/analysis/` are the actual source of truth for behavior** — precise, unambiguous contracts matter more here than in a team that can clarify things verbally. Do not invent domain rules, entity shapes, service boundaries, or event contracts that aren't in these docs.

## 1. Read this first: the spec set, in order

Read these before implementing anything. They supersede ad hoc judgment calls; if you find a gap or contradiction between them (it has happened before), stop and surface it instead of silently picking an interpretation — see §6.

1. `docs/analysis/domain.md` — the canonical entity/attribute/relationship model. Every entity, field, and relationship you implement must trace back to this file.
2. `docs/analysis/services.md` — the 6 deployable services, what each owns, why they're grouped this way, the database-per-service plan, and the **Cross-Service Data Consistency Strategy** (local read-replicas kept in sync via domain synchronization events — this governs how services validate foreign references across service boundaries).
3. `docs/analysis/events.md` — the event catalog: domain synchronization events (in full detail, with payloads) and the wider anticipated integration event list (first pass, payloads TBD per service).
4. `docs/analysis/contracts.md` — the MVP-scoped command/query catalog per service. This is the closest thing to an API contract per service; extend it as you go, don't bypass it.
5. `docs/analysis/api-led/system-apis.md` — the original System API derivation. **Marked superseded where it conflicts with `domain.md`/`services.md`/`contracts.md`** — those three win on any discrepancy. Still useful for the API-led reasoning and the Integration-Facing System APIs section (Bank Feed Integration = later phase; Document Extraction = in-MVP, as an adapter inside Bookkeeping & Planning).
6. `docs/analysis/mvp/screens-and-features.md` — the 11 MVP screens and their required features. This is ground truth for what's actually in scope for the frontend; if a screen or feature isn't listed here, it isn't MVP.
7. `docs/analysis/mvp/scope-decisions.md` — MVP-specific scope cuts and decisions that don't belong in the permanent domain model (currency, tax handling, reconciliation scope, OCR import scope, project/user scope, test rigor, delivery model). Read this alongside `domain.md` — it explains *why* certain fields/flows exist or are simplified.
8. `docs/analysis/mvp/workplan.md` — the approved 6-phase MVP delivery plan (vertical slices, spec-driven, each phase end-to-end testable through the frontend). **This is the mandatory implementation sequence** — see §4.

Then the code-level standards, which govern *how* code is written once you know *what* to build:

9. `docs/standards/architecture-guidelines.md` — API-led connectivity, DDD, CQRS, transactional outbox/inbox, event-driven integration, Dapr sidecar pattern. Note: its "Required Delivery Sequence" (horizontal/layered) is **not** what this project follows — `workplan.md`'s vertical-slice phase order takes precedence, because every phase must be frontend-E2E-testable on its own. Everything else in this file (DDD/CQRS/outbox conventions, event naming) still applies as written.
10. `docs/standards/backend-api-code-guidelines.md` — service folder structure (Api/Application/Domain/Infrastructure/Tests), CQRS enforcement, EF Core + Postgres conventions, DTO mapping, validation, outbox interceptor, Problem Details, idempotency/optimistic concurrency, unit testing, review checklist.
11. `docs/standards/frontend-code-guidelines.md` — project structure, component/state conventions, Experience API client usage, TanStack Query patterns, forms/validation, routing, accessibility, formatting of financial data, styling, testing.
12. `docs/standards/tech-stack.md` — exact package/version baseline and the "Product Stack To Apply" (packages that are planned but not yet installed — see §3).

## 2. Current implementation state (verify before assuming otherwise)

As of this writing, Phase 1 is complete and Task 2.2 has established the initial Ledger service infrastructure:

- `src/Contapop.Application.slnx` is an Aspire solution with eight projects: `Contapop.Application.AppHost`, `Contapop.Experience.Api`, `Contapop.Experience.Api.Tests`, `Contapop.Identity.Service`, `Contapop.Identity.Service.Tests`, `Contapop.Ledger.Service`, `Contapop.Ledger.Service.Tests`, and `frontend/frontend.esproj`. Identity & Tenancy has its initial schema and provisioning/authentication flows. Financial Accounts & Ledger has its initial schema, migration, project inbox, and local Project replica; its bank-account, card, and transaction API features are future Phase 2 work.
- `Contapop.Application.AppHost/AppHost.cs` wires up Redis (`AddRedis("cache")`), one Aspire-managed PostgreSQL server with five service-owned databases, the Identity & Tenancy service, the Ledger service, the Experience API, and the Vite frontend.
- `Contapop.Application.Server` is unused default ASP.NET Core Minimal API template output. It still has a placeholder `/api/weatherforecast` endpoint, which is scaffold cruft and should be deleted rather than extended if that project is ever brought back into the application graph.
- `Contapop.Identity.Service` contains the Identity & Tenancy EF Core model, migrations, transactional-outbox schema, provisioning, cookie authentication, and user updates. `Contapop.Identity.Service.Tests` applies migrations to Testcontainers PostgreSQL and covers those flows. Outbox dispatch is still future work.
- `Contapop.Ledger.Service` contains the initial Financial Accounts & Ledger EF Core model, migration, durable inbox, and `project_replica` local read model. `Contapop.Ledger.Service.Tests` validates migrations and project-event replication against Testcontainers PostgreSQL.
- `src/frontend` is a React 19.2 + TypeScript 5.9 + Vite 8 app with React Router, TanStack Query, React Hook Form, and Zod installed. Radix UI is not installed; add it only when a task needs an accessible primitive it provides.
- Do not trust a stale description of the repo (including anything an older version of this file said) over what's actually in `src/` — if something here turns out to be inaccurate, check the source and treat this file as needing an update, not the other way around.

## 3. Verified commands

Only use commands that are actually wired up. Don't invent a `dotnet test` command — no test project exists yet; add one (per `backend-api-code-guidelines.md`'s xUnit conventions) before a task needs to run backend tests.

Backend / orchestration (from `src/`):
```
dotnet build Contapop.Application.slnx
dotnet test Contapop.Application.slnx
dotnet run --project Contapop.Application.AppHost   # starts the full Aspire app graph (server + Redis + frontend)
powershell -ExecutionPolicy Bypass -File ../scripts/test-event-backbone.ps1  # exercises Identity outbox -> Dapr -> Ledger replica
```

The AppHost runs Identity & Tenancy and Ledger with Dapr sidecars. Install the Dapr CLI before starting the full graph; the `CommunityToolkit.Aspire.Hosting.Dapr` package is pinned to the compatible `13.5.1-beta.748` release while Aspire 13.5.x reaches a stable Dapr-hosting integration.

Frontend (from `src/frontend`):
```
npm run dev        # Vite dev server
npm run build       # tsc -b && vite build
npm run lint        # eslint .
npm run preview
npm run test        # Vitest component tests
npm run test:e2e    # Playwright critical-path tests; requires the local Aspire stack and PLAYWRIGHT_PILOT_EMAIL/PASSWORD/NAME
```

When a task introduces a new project type (a service's test project, an E2E Playwright project, etc.), add its build/run/test commands to this section in the same commit/PR that adds the project, so this file stays accurate.

## 4. Implementation process: spec-driven, phase-then-task, vertical slices

Follow `docs/analysis/mvp/workplan.md` as the mandatory sequence. Do not reorder phases or skip ahead because a later phase looks easier or more interesting.

- **Phases are vertical slices**, not architectural layers: each phase cuts through DB → System API → Experience API → a real React screen → a Playwright end-to-end test. This is intentional and departs from `architecture-guidelines.md`'s horizontal "Required Delivery Sequence" — every phase must be demonstrable and testable through the actual frontend against the actual local stack (Aspire + Postgres + Playwright), not just "the backend returns 200."
- **Each phase is broken into tasks**, and each task must be:
  - independently implementable,
  - independently testable (unit and/or integration as appropriate — see `backend-api-code-guidelines.md`'s Unit Testing section and `frontend-code-guidelines.md`'s Testing section),
  - independently committable,
  - allowed to depend only on tasks that are already completed, tested, and committed — never on a task that is planned but not yet done.
- Before starting a phase's tasks, check whether that phase's task breakdown already exists (look for a tasks file alongside `workplan.md`, or ask if none exists yet). Do not invent your own phase ordering or task granularity from scratch if a breakdown has already been agreed.
- When a task is done: run its tests, confirm the E2E path it belongs to still works, then commit. Don't batch multiple tasks into one uncommitted pile of work — the whole point of the breakdown is small, verifiable, revertable steps.
- Test rigor for MVP is "critical paths only" per `scope-decisions.md` — don't over-invest in exhaustive test matrices for a small private pilot, but don't skip testing the money-affecting paths (transactions, invoices, payments, reconciliation) either.

## 5. Architectural rules that apply to every task

These are the load-bearing decisions from the analysis phase. Violating them silently is the most likely way to introduce a defect an agent won't notice on its own:

- **Service boundaries**: implement inside the 6 services defined in `services.md` (Identity & Tenancy, Financial Accounts & Ledger, Billing & Invoicing, Bookkeeping & Planning, Reporting, Experience API). Don't add a cross-service database call — cross-service data needs go through the local read-replica + domain synchronization event mechanism (see next point), never a synchronous call into another service's database.
- **Cross-service consistency**: Identity & Tenancy (for `Project`) and Financial Accounts & Ledger (for `Transaction`) are the two reference-data hubs. Any service that needs to validate a reference to a Project or a Transaction at write time does so against its own local read-replica, kept current via the domain synchronization events cataloged in `events.md`. This is eventually consistent by design — document any new staleness-sensitive flow you introduce. A compensation/saga mechanism (Dapr Workflow) for handling missed/late events is explicitly deferred — don't build one speculatively; flag the need if a task seems to require it.
- **Soft delete / tombstones**: any entity that's referenced across services (`Project`, `Transaction`) is archived, never hard-deleted, so replicated foreign keys never dangle.
- **CQRS**: commands (state-changing, imperative names like `ArchiveTransaction`) and queries (read-only) are strictly separated per `backend-api-code-guidelines.md` and cataloged in `contracts.md`. Don't add a command or query that isn't in `contracts.md` without adding it there first (or flagging the gap).
- **Outbox/inbox**: every integration event listed in `events.md` is published via the transactional outbox and consumed via the standard inbox pattern (dedup by `event_id`, apply only if `aggregate_version` is newer). Follow the envelope shape defined in `events.md` exactly.
- **Tenancy**: every entity and event is tenant-scoped (`tenant_id`). MVP has a single implicit Project per Tenant and single-owner access (no multi-user roles yet) per `scope-decisions.md` — don't build a roles/permissions system that isn't asked for.
- **PII/PCI minimization**: Payment Cards store a user-entered `label` only (e.g. "Visa ending 1234") — never a real card number. Bank Accounts retain the documented `account_number` field; protect it as sensitive financial data and do not expose it outside the owning service's authorized API. Never add a field that stores a real card number.
- **OCR/document import**: PDF invoice/receipt import always produces a draft that a user must explicitly confirm — never auto-finalize extracted financial data into a real Expense/Revenue/Invoice record.
- **Money handling**: integer minor units per `tech-stack.md`, not floating point.
- **Compliance features** (Facturae, SII, Verifactu) are explicitly out of MVP scope per `scope-decisions.md`. Don't build toward them without being asked — but don't do anything that would make adding them later structurally harder than necessary (e.g. keep tax fields on `Invoice or Ticket` as already modeled in `domain.md`).

## 6. When you find a gap or contradiction

This has happened repeatedly during the analysis phase (e.g. `Transaction` initially had no link back to `Invoice`/`Payment`/`Expense` despite the screens implying one; `screens-and-features.md` and the old `system-apis.md` disagreed on whether PDF import was MVP). When it happens during implementation:

1. Don't silently pick an interpretation and move on.
2. Don't guess at a "reasonable default" for anything that affects the domain model, service boundaries, event contracts, or MVP scope — those are Gerardo's decisions.
3. Surface the gap clearly: what you found, why it's a contradiction or omission, and (if you have one) a grounded recommendation with trade-offs — the same way this project's own analysis docs record resolved decisions.
4. Once resolved, update every affected doc (`domain.md`, `services.md`, `events.md`, `contracts.md`, `scope-decisions.md`, `workplan.md` as relevant) so the specs stay authoritative — don't let the answer live only in a commit message or chat history.

Small, purely technical implementation details that don't affect the documented domain/contracts (e.g. exact folder naming within the conventions already given in the code-style guides) don't need to be escalated — use the code-style guides and existing code as precedent.

## 7. Keeping this file current

If you change something this file asserts as fact (a command, the scaffold state, which packages are installed, which services exist as real projects), update the relevant section in the same change. This file is only useful if it stays accurate — an agent trusting a stale "no database yet" line after Postgres has been added will make bad decisions.
