# Graph Report - src  (2026-09-13)

## Corpus Check
- 269 files · ~72,979 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 96 nodes · 155 edges · 7 communities
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `2a803314`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- DateTimeOffset
- FinancialRecord
- Plan
- PlannedLine
- OutboxMessage
- BookkeepingEntities.cs
- Contapop.Bookkeeping.Service.csproj

## God Nodes (most connected - your core abstractions)
1. `FinancialRecord` - 25 edges
2. `Plan` - 22 edges
3. `PlannedLine` - 18 edges
4. `OutboxMessage` - 15 edges
5. `IdempotencyRecord` - 9 edges
6. `Expense` - 5 edges
7. `Revenue` - 5 edges
8. `PlannedExpense` - 3 edges
9. `PlannedRevenue` - 3 edges
10. `net10.0` - 1 edges

## Surprising Connections (you probably didn't know these)
- `Expense` --inherits--> `FinancialRecord`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs → Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs  _Bridges community 1 → community 0_
- `IdempotencyRecord` --references--> `Guid`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs →   _Bridges community 0 → community 5_
- `OutboxMessage` --references--> `Guid`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs →   _Bridges community 0 → community 4_
- `Plan` --references--> `Guid`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs →   _Bridges community 0 → community 2_
- `PlannedLine` --references--> `Guid`  [EXTRACTED]
  Contapop.Bookkeeping.Service/Infrastructure/Persistence/BookkeepingEntities.cs →   _Bridges community 0 → community 3_

## Import Cycles
- None detected.

## Communities (7 total, 0 thin omitted)

### Community 0 - "DateTimeOffset"
Cohesion: 0.23
Nodes (6): Expense, Revenue, DateOnly, DateTimeOffset, DocumentExtractionResult, Guid

### Community 1 - "FinancialRecord"
Cohesion: 0.12
Nodes (15): FinancialRecord, AmountMinor, Category, ConfirmedAt, CreatedAt, Date, Id, ImportSource (+7 more)

### Community 2 - "Plan"
Cohesion: 0.14
Nodes (13): Plan, AllocatedAmountMinor, CreatedAt, Description, EndDate, Id, ProjectId, StartDate (+5 more)

### Community 3 - "PlannedLine"
Cohesion: 0.14
Nodes (13): PlannedExpense, PlannedLine, AmountMinor, Category, CreatedAt, Date, Id, PlanId (+5 more)

### Community 4 - "OutboxMessage"
Cohesion: 0.17
Nodes (12): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventName (+4 more)

### Community 5 - "BookkeepingEntities.cs"
Cohesion: 0.20
Nodes (8): IdempotencyRecord, CreatedAt, Key, Operation, Result, TenantId, PlannedRevenue, Contapop.Bookkeeping.Service.Infrastructure.Persistence

### Community 6 - "Contapop.Bookkeeping.Service.csproj"
Cohesion: 0.22
Nodes (8): net10.0, ClosedXML (0.105.0), Dapr.AspNetCore (1.15.4), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web

## Knowledge Gaps
- **62 isolated node(s):** `net10.0`, `ClosedXML (0.105.0)`, `Dapr.AspNetCore (1.15.4)`, `Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11)`, `Microsoft.AspNetCore.OpenApi (10.0.11)` (+57 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 62 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FinancialRecord` connect `FinancialRecord` to `DateTimeOffset`, `BookkeepingEntities.cs`?**
  _High betweenness centrality (0.263) - this node is a cross-community bridge._
- **Why does `Plan` connect `Plan` to `DateTimeOffset`, `PlannedLine`, `BookkeepingEntities.cs`?**
  _High betweenness centrality (0.220) - this node is a cross-community bridge._
- **Why does `PlannedLine` connect `PlannedLine` to `DateTimeOffset`, `BookkeepingEntities.cs`?**
  _High betweenness centrality (0.203) - this node is a cross-community bridge._
- **What connects `net10.0`, `ClosedXML (0.105.0)`, `Dapr.AspNetCore (1.15.4)` to the rest of the system?**
  _62 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `FinancialRecord` be split into smaller, more focused modules?**
  _Cohesion score 0.12418300653594772 - nodes in this community are weakly interconnected._
- **Should `Plan` be split into smaller, more focused modules?**
  _Cohesion score 0.14285714285714285 - nodes in this community are weakly interconnected._
- **Should `PlannedLine` be split into smaller, more focused modules?**
  _Cohesion score 0.14285714285714285 - nodes in this community are weakly interconnected._