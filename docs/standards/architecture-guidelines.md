# Architecture Guidelines

## Scope And Baseline

Contapop is a freelance account-management application. The current implementation is a single API service, React frontend, Redis cache, and .NET Aspire AppHost:

- `src/Contapop.Application.Server` is the ASP.NET Core API and production host for frontend static files.
- `src/frontend` is the React and Vite single-page application.
- `src/Contapop.Application.AppHost` uses .NET Aspire to run the API, frontend, and Redis locally.

The target architecture is a distributed system with independently deployable services connected through API-led connectivity and domain synchronization events. The current `Contapop.Application.Server` project is a scaffold to be decomposed as capabilities are implemented; it is not the intended long-term boundary. Dapr integration is introduced behind application-owned abstractions so its APIs do not enter the domain model.

## Core Principles

- Model business rules in the domain, not in HTTP endpoints, React components, EF Core configuration, or Dapr handlers.
- Deploy bounded contexts as independently owned services where they have distinct data ownership, lifecycle, scaling, reliability, or security requirements.
- Connect services through API-led interfaces and published integration events. Do not permit direct database access across service boundaries.
- Use PostgreSQL as the transactional system of record. Redis and Dapr state stores are not authoritative stores for financial data.
- Make write processing strongly consistent within one aggregate and eventually consistent outside it.
- Treat all delivery as at-least-once. Consumers, projections, audit processing, and orchestrations must be idempotent.
- Maintain correlation, causation, tenant, actor, and trace context across commands, domain events, integration events, and Dapr calls.
- Never let external messages or UI request models become domain entities. Translate them at the application boundary.

## Module Structure

Each bounded context owns its domain, application use cases, infrastructure adapters, database schema, and API endpoint mapping. Start with services such as `Identity`, `Clients`, `Invoicing`, `Expenses`, `Payments`, and `Reporting`. A service may contain more than one deployable API only when those APIs have the same data owner and release lifecycle.

```text
src/
  Contapop.Invoicing.Service/
    Domain/
    Application/
    Infrastructure/
    Api/
  Contapop.Expenses.Service/
    Domain/
    Application/
    Infrastructure/
    Api/
  Contapop.Reporting.Service/
    Application/
    Infrastructure/
    Api/
  Contapop.Experience.Api/
  Contapop.Application.AppHost/
  frontend/
```

- `Domain` references no infrastructure, web, Dapr, EF Core, or serialization packages.
- `Application` coordinates domain behavior through interfaces and owns command/query handlers. It may reference the domain.
- `Infrastructure` implements repositories, service-local database access, Dapr adapters, durable outbox/inbox storage, and projection persistence.
- `Api` maps HTTP requests to commands and queries. It contains authorization, request parsing, and response mapping, but no business rules.
- Shared libraries contain only versioned technical primitives, such as observability and event-envelope contracts. Do not share domain models, persistence entities, or business logic between services.

## API-Led Connectivity

Services expose contracts through three API layers. Each layer is independently versioned, documented with OpenAPI or AsyncAPI as applicable, protected by authorization, and observed with OpenTelemetry.

| API layer | Consumers | Responsibility | Contapop examples |
| --- | --- | --- | --- |
| System APIs | Internal services and controlled technical consumers | Provide stable access to a service's system of record and its core capabilities. They own validation, authorization, and business operations for their bounded context. | Invoicing System API, Payments System API, Clients System API |
| Process APIs | Experience APIs, workflows, and internal automation | Compose system API capabilities and event-driven state into reusable business processes. They coordinate but do not take ownership of another service's data. | Invoice collection process, month-end close process |
| Experience APIs | React web application and future client channels | Shape data and interactions for a specific user experience. They aggregate process and system APIs and do not duplicate domain rules. | Freelance-accounting web Experience API |

- The React application calls the Experience API, not domain system APIs directly.
- Process APIs invoke System APIs through published synchronous contracts when an immediate result is required; use integration events for asynchronous synchronization.
- A System API is the only synchronous API allowed to mutate the data it owns. External services request changes through that API or a documented command endpoint.
- Do not build a generic API gateway that exposes every downstream endpoint. The Experience API is a product-specific boundary, not a transparent proxy.
- Provide OpenAPI for HTTP APIs and AsyncAPI for topics, event envelopes, schemas, consumer expectations, retry behavior, and deprecation schedules.
- Version additive changes compatibly. Publish a new major API or event version for breaking changes and retire the predecessor through an explicit consumer migration period.

## Domain-Driven Design

### Bounded Contexts

Each module defines its ubiquitous language, ownership, data schema, APIs, events, and transactional boundaries. Cross-context access must use a query API, a published integration event, or an explicit application contract. A module must not directly query or update another module's tables.

Examples of aggregate roots include `Client`, `Invoice`, `Expense`, and `Payment`. An aggregate:

- Enforces its invariants through methods and value objects.
- Is loaded and persisted as a consistency boundary.
- Emits domain events only after an accepted state transition.
- Does not publish messages, call Dapr, access repositories, or depend on current HTTP context.

Store monetary amounts as integer minor units plus ISO 4217 currency. Use `DateOnly` for accounting dates and UTC `DateTimeOffset` values for event and audit timestamps.

### Domain Events

Domain events describe facts that have occurred inside a module, for example `InvoiceIssued` or `ExpenseRecorded`. They are immutable, past tense, and contain the minimum data necessary for in-process reactions.

- Collect domain events on aggregates during command execution.
- Persist the aggregate changes and translated outbox messages in the same database transaction.
- Do not use a domain event as a public integration contract. Map it to a versioned integration event at the application or infrastructure boundary.
- Name integration events as `{bounded-context}.{event-name}.v{major}`, for example `invoicing.invoice-issued.v1`.

## CQRS

CQRS separates intent to change state from reads. It does not require separate databases or services.

### Commands

A command expresses one business intent, such as `IssueInvoice` or `RecordExpense`.

- Commands are validated, authorized, and handled once.
- Command handlers load the required aggregate, invoke domain behavior, and commit one transaction.
- Commands return an acknowledgement or the changed resource identifier and version, not a read model assembled from unrelated modules.
- Require an idempotency key for externally retried write requests. Store the key and resulting response for a defined retention period.
- Use optimistic concurrency for aggregate updates. Return a conflict response when the client acts on stale state.

### Queries

A query has no side effects and reads a purpose-built read model.

- Queries may use EF Core projections, SQL, or a dedicated read-store adapter; they must not load aggregates merely to render a screen.
- Keep query DTOs separate from domain entities and command contracts.
- Return data filtered by tenant and authorization scope at the query boundary.
- Cache only query results that tolerate staleness. Invalidate or version cache entries from projection updates rather than assuming immediate global consistency.

## Projections And Auditing

Projections transform committed events into query-optimized read models. They enable dashboard totals, invoice lists, client balances, and reports without weakening aggregate boundaries.

- A projection is owned by the module that owns its read concern and has a stable projection name and schema version.
- Persist a checkpoint or processed-message record with each projection update.
- Projection handlers must be deterministic and idempotent. Replaying the same event must not change the final result after its first successful application.
- Rebuildable projections must be reproducible from retained integration events or a documented snapshot and replay process.
- Never use a projection as a source of truth for a command decision that requires strong consistency.

Auditing is an append-only projection of business and security events, not an EF Core change-tracker dump.

- Each auditable entry includes event ID, event type and version, aggregate type and ID, tenant ID, actor ID or system actor, correlation ID, causation ID, occurred-at UTC timestamp, and a business-safe change summary.
- Capture before/after values only for approved, non-secret fields. Do not place passwords, access tokens, bank credentials, or unnecessary personal data in events or audit records.
- Audit records are immutable. Corrections are additional events, never updates that rewrite history.
- Apply retention, access control, and export policies appropriate to financial and personal data.

## Outbox And Inbox

### Transactional Outbox

Every integration event must be written to an outbox table in the same PostgreSQL transaction as its aggregate change. A failed transaction emits no event; a committed change always has a durable event to publish.

The outbox record must include a unique message ID, event name and version, serialized payload, metadata, occurred-at timestamp, correlation ID, causation ID, tenant ID, publish attempts, and publication status.

- A background dispatcher claims pending rows safely, publishes through the Dapr pub/sub API, and marks rows as dispatched only after acknowledgement.
- Retry transient publication failures with backoff. Move exhausted messages to a visible failed state with operational alerts; do not silently discard them.
- Maintain ordering only where it is needed: partition messages by aggregate ID and include the aggregate version. Consumers must tolerate reordered messages across aggregates.
- Retain dispatched outbox records for a documented replay and support window before archival or deletion.

### Inbox

Each integration-event consumer stores the message ID and consumer name in an inbox as part of the transaction that applies the message.

- Reject a message already completed by that consumer.
- Record processing failures and allow retry without duplicating side effects.
- The inbox must be durable and unique on `(consumer_name, message_id)`.
- Incoming Dapr event handlers acknowledge success only after the inbox transaction and all local writes are complete.

The outbox and inbox provide effectively-once business processing over at-least-once transport. They do not promise exactly-once delivery.

## Event-Driven Integration

Use integration events for asynchronous domain synchronization between services. Typical flows include updating reporting projections after `InvoiceIssued` and scheduling payment reminders after `InvoiceOverdue`.

- Publish only events that other modules are permitted to depend on. Event payloads are immutable contracts and must be versioned.
- Prefer additive event changes. Publish a new major event name or version for breaking changes and support consumers through an explicit migration window.
- Include stable identifiers and business facts, not serialized aggregate graphs or implementation-specific database fields.
- Subscribe through Dapr pub/sub endpoints that delegate immediately to an application-level consumer.
- HTTP request/response is appropriate for immediate user feedback; events are appropriate for asynchronous synchronization. Do not make commands wait for every downstream projection or subscriber.

## Orchestrator Pattern

Use an orchestrator for a long-running workflow that spans multiple services, commands, retries, waiting periods, or compensations. Do not introduce an orchestrator for a simple single-service transaction.

Examples include invoice delivery and payment follow-up: issue an invoice, generate a document, request delivery, wait for payment or a due date, then send reminders or record a failure.

- Implement workflow state, timers, retries, and compensation with Dapr Workflow through an application-owned `IWorkflowOrchestrator` abstraction.
- Orchestrators coordinate; they do not contain aggregate invariants. Activities invoke idempotent commands through application interfaces or APIs.
- Start workflows from successfully processed integration events. Use the event ID or aggregate ID plus workflow type as a deterministic workflow ID when only one active workflow is allowed.
- Persist workflow progress through the workflow runtime and publish resulting business facts through the normal outbox. Do not publish directly from an activity without outbox protection.
- Make every activity idempotent and give it an explicit timeout, retry policy, and compensation or terminal-failure behavior.
- Surface workflow status through a query projection; do not expose Dapr runtime state directly to the frontend.

## Dapr Sidecar Pattern

Dapr runs as a sidecar beside every .NET service process. A service communicates with its local Dapr sidecar using the Dapr .NET SDK; it never connects directly to a pub/sub broker, distributed-lock provider, or workflow runtime.

The current Aspire AppHost already manages Redis and the scaffold API. Extend it to run each service with a Dapr sidecar and component configuration during local development. Use equivalent Dapr component manifests and managed backing services in deployed environments.

| Capability | Dapr building block | Rule |
| --- | --- | --- |
| Cache | State store backed by Redis | Cache query responses or derived data only. Set TTLs and invalidate from projection updates. Do not use it for authoritative accounting state. |
| Distributed locks | Distributed lock API backed by Redis | Use only to coordinate short-lived cross-process work such as an outbox dispatcher lease. Keep database constraints and idempotency as the correctness mechanism. |
| Outbox delivery | Pub/sub | The outbox dispatcher publishes integration events through a named Dapr component and topic. |
| Inbox delivery | Pub/sub subscription endpoint | The API receives events from the sidecar and applies the inbox transaction before acknowledging delivery. |
| Orchestration | Dapr Workflow | Use for durable, long-running, multi-step workflows. |
| Secrets | Secret store | Retrieve deployment secrets through Dapr configuration where appropriate; never commit secrets or component credentials. |

- Name Dapr components, topics, consumer names, and workflow types explicitly and consistently. Keep names environment-neutral; inject environment-specific connection settings through secrets and configuration.
- Add Dapr health to application readiness checks. A service that requires pub/sub or workflow processing must report degraded readiness when its required sidecar dependency is unavailable.
- Propagate W3C trace context and OpenTelemetry correlation through Dapr metadata. Log message IDs, aggregate IDs, workflow IDs, and retries as structured properties.
- Define local component manifests under source control without credentials. Place secrets in ignored `.env` files, .NET user secrets, or the deployment secret store.

## Required Delivery Sequence

1. Add PostgreSQL, EF Core migrations, and an independently owned database per service to the Aspire environment.
2. Split the scaffold API into an initial System API service, starting with clients or invoicing, and introduce its domain, application, infrastructure, and API layers.
3. Add the web Experience API and connect the React frontend exclusively through it; publish OpenAPI contracts for both API layers.
4. Add command idempotency, aggregate concurrency, domain events, transactional outbox, and a single auditable projection in the initial service.
5. Add Dapr sidecar configuration per service, pub/sub, inbox processing, Redis-backed cache, and distributed-lock support behind interfaces; publish AsyncAPI event contracts.
6. Add Process APIs, remaining projections, and a Dapr Workflow-based orchestrator only for proven multi-service workflows.
7. Test contract compatibility, service authorization, command invariants, outbox atomicity, duplicate delivery, projection replay, and workflow retry or compensation paths using PostgreSQL and Dapr-capable integration environments.
