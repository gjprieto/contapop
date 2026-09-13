# Graph Report - src  (2026-09-13)

## Corpus Check
- 281 files · ~78,137 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 452 nodes · 951 edges · 22 communities (16 shown, 5 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 12 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `88f73de5`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- DateTimeOffset
- FinancialRecord
- Plan
- PlannedLine
- OutboxMessage
- IdempotencyRecord
- Contapop.Bookkeeping.Service.csproj
- ReconciliationOperation
- RecordResult
- .Map
- FinancialRecordReconciliationCoordinator
- plans-api.ts
- financial-records-page.tsx
- .ParseAsync
- Contapop.Experience.Api/Program.cs
- .ExtractAsync
- DbSet
- HttpContext
- IEndpointRouteBuilder
- IResult
- IDocumentExtractionAdapter

## God Nodes (most connected - your core abstractions)
1. `ReconciliationOperation` - 37 edges
2. `RecordResult` - 34 edges
3. `FinancialRecordCommandHandler` - 28 edges
4. `FinancialRecord` - 25 edges
5. `Plan` - 22 edges
6. `FinancialRecordResponse` - 19 edges
7. `PlannedLine` - 18 edges
8. `PaymentReconciliationCoordinator` - 16 edges
9. `FinancialRecordFileImportException` - 15 edges
10. `OutboxMessage` - 15 edges

## Surprising Connections (you probably didn't know these)
- `FinancialRecordCommandHandler` --references--> `IDocumentExtractionAdapter`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Application/Commands/FinancialRecordCommandHandler.cs → Contapop.Bookkeeping.Service/Application/Abstractions/IDocumentExtractionAdapter.cs
- `PaymentReconciliationCoordinator` --references--> `ReconciliationDbContext`  [EXTRACTED]
  Contapop.Reconciliation.Service/Reconciliation/PaymentReconciliationCoordinator.cs → Contapop.Reconciliation.Service/Infrastructure/Persistence/ReconciliationDbContext.cs
- `PlansPage()` --indirect_call--> `archivePlan()`  [INFERRED]
  frontend/src/features/plans/pages/plans-page.tsx → frontend/src/features/plans/api/plans-api.ts
- `AzureDocumentExtractionAdapter` --implements--> `IDocumentExtractionAdapter`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/DocumentExtraction/AzureDocumentExtractionAdapter.cs → Contapop.Bookkeeping.Service/Application/Abstractions/IDocumentExtractionAdapter.cs
- `FinancialRecordReconciliationCoordinator` --references--> `ReconciliationDbContext`  [EXTRACTED]
  Contapop.Reconciliation.Service/Reconciliation/FinancialRecordReconciliationCoordinator.cs → Contapop.Reconciliation.Service/Infrastructure/Persistence/ReconciliationDbContext.cs

## Import Cycles
- None detected.

## Communities (22 total, 5 thin omitted)

### Community 0 - "DateTimeOffset"
Cohesion: 0.23
Nodes (6): Expense, Revenue, DateOnly, DateTimeOffset, DocumentExtractionResult, Guid

### Community 1 - "FinancialRecord"
Cohesion: 0.12
Nodes (15): FinancialRecord, AmountMinor, Category, ConfirmedAt, CreatedAt, Date, Id, ImportSource (+7 more)

### Community 2 - "Plan"
Cohesion: 0.15
Nodes (13): Plan, AllocatedAmountMinor, CreatedAt, Description, EndDate, Id, ProjectId, StartDate (+5 more)

### Community 3 - "PlannedLine"
Cohesion: 0.12
Nodes (15): PlannedExpense, PlannedLine, AmountMinor, Category, CreatedAt, Date, Id, PlanId (+7 more)

### Community 4 - "OutboxMessage"
Cohesion: 0.17
Nodes (12): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventName (+4 more)

### Community 5 - "IdempotencyRecord"
Cohesion: 0.33
Nodes (6): IdempotencyRecord, CreatedAt, Key, Operation, Result, TenantId

### Community 6 - "Contapop.Bookkeeping.Service.csproj"
Cohesion: 0.22
Nodes (8): net10.0, ClosedXML (0.105.0), Dapr.AspNetCore (1.15.4), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web

### Community 7 - "ReconciliationOperation"
Cohesion: 0.07
Nodes (36): ReconciliationOperation, Attempts, ClaimId, ClaimVersion, CreatedAt, DependentId, DependentType, ExpectedDependentVersion (+28 more)

### Community 8 - "RecordResult"
Cohesion: 0.12
Nodes (27): ConfirmImportedFinancialRecordCommand, CreateFinancialRecordCommand, DeletedFinancialRecordResponse, DeleteFinancialRecordCommand, FinancialRecordCommandHandler, FinancialRecordResponse, ImportedFinancialRecordResponse, ImportedFinancialRecordsResponse (+19 more)

### Community 9 - ".Map"
Cohesion: 0.09
Nodes (27): ConfirmImportedFinancialRecordRequest, CreateFinancialRecordRequest, FinancialRecordEndpoints, FinancialRecordListItem, FinancialRecordPagedResponse, ImportFinancialRecordsResponse, ImportedCount, ReconcileFinancialRecordRequest (+19 more)

### Community 10 - "FinancialRecordReconciliationCoordinator"
Cohesion: 0.05
Nodes (38): BackgroundService, ReconciliationDbContext, Operations, DbSet, Program, FinancialRecordReconciliationCoordinator, ReservationResponse, StartFinancialRecordReconciliationRequest (+30 more)

### Community 11 - "plans-api.ts"
Cohesion: 0.24
Nodes (17): addPlannedLine(), archivePlan(), createPlan(), getPlan(), getPlans(), getPlanVsActual(), Plan, PlannedLine (+9 more)

### Community 12 - "financial-records-page.tsx"
Cohesion: 0.09
Nodes (36): ExpensesPage(), confirmRecord(), createRecord(), deleteRecord(), getRecords(), getTransactions(), importRecords(), key() (+28 more)

### Community 13 - ".ParseAsync"
Cohesion: 0.15
Nodes (19): Amount, Category, FinancialRecordColumnMapping, FinancialRecordFileImporter, FinancialRecordFileImportException, SkippedRows, FinancialRecordFileImportParseResult, ImportFinancialRecordRow (+11 more)

### Community 14 - "Contapop.Experience.Api/Program.cs"
Cohesion: 0.16
Nodes (15): ClaimsPrincipal, AuthenticatedUserResponse, DateTimeOffset, Guid, IConfiguration, CurrentUserResponse, InternalJwtIssuer, LoginRequest (+7 more)

### Community 15 - ".ExtractAsync"
Cohesion: 0.11
Nodes (21): DocumentExtractionConfigurationException, DocumentExtractionException, DocumentExtractionResult, IDocumentExtractionAdapter, CancellationToken, DateOnly, ReadOnlyMemory, Task (+13 more)

## Knowledge Gaps
- **103 isolated node(s):** `ImportedCount`, `Value`, `Error`, `Contapop.Bookkeeping.Service.Infrastructure.DocumentExtraction`, `Program` (+98 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 170 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FinancialRecordCommandHandler` connect `RecordResult` to `.Map`, `.ExtractAsync`?**
  _High betweenness centrality (0.050) - this node is a cross-community bridge._
- **Why does `FinancialRecord` connect `FinancialRecord` to `DateTimeOffset`, `PlannedLine`?**
  _High betweenness centrality (0.041) - this node is a cross-community bridge._
- **Why does `RecordResult` connect `RecordResult` to `.Map`?**
  _High betweenness centrality (0.037) - this node is a cross-community bridge._
- **What connects `ImportedCount`, `Value`, `Error` to the rest of the system?**
  _103 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `FinancialRecord` be split into smaller, more focused modules?**
  _Cohesion score 0.12418300653594772 - nodes in this community are weakly interconnected._
- **Should `PlannedLine` be split into smaller, more focused modules?**
  _Cohesion score 0.12418300653594772 - nodes in this community are weakly interconnected._
- **Should `ReconciliationOperation` be split into smaller, more focused modules?**
  _Cohesion score 0.06966618287373004 - nodes in this community are weakly interconnected._