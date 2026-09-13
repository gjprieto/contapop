# Graph Report - src  (2026-09-13)

## Corpus Check
- 274 files · ~72,915 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2840 nodes · 5837 edges · 173 communities (148 shown, 14 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 204 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- TransactionEndpoints
- BlobServiceClient
- PlanEndpoints
- ReconciliationClaimEndpoints
- CounterpartyEndpoints
- Amount
- .OnModelCreating()
- invoices-api
- 20260912205349 InitialBookkeepingReplication
- .Retry()
- IReconciliationClaimValidator
- Appx
- IDomainEvent
- FinancialRecordCommandHandler
- IClock
- ProjectReplicationEndpoints
- useBankAccounts()
- AccountEndpoints
- BackgroundService
- 20260906133029 InitialIdentitySchema
- ProvisionTenantCommand
- InvoiceCommandHandler
- accounts-api
- Contapop.Bookkeeping.Service/Api/ReplicationEndpoints
- LedgerDbContext
- Contapop.Billing.Service/Application/Replication/Projec
- AccountCommandHandler
- tsconfig.app.json
- EventPayload
- DocumentExtractionResult
- EventPayload
- Contapop.Ledger.Service/Application/Abstractions/IInteg
- 20260908175710 InitialReconciliationSchema
- ReconciliationOperation
- Contapop.Identity.Service/Application/Abstractions/IInt
- OutboxMessage
- Transaction
- IntegrationEventEnvelope
- ExperienceUserIntegrationTests
- UpdateUserPreferencesCommand
- IdentityCredential
- eslint
- tsconfig.node.json
- PaymentCommandHandler
- FinancialRecordEndpoints
- BookkeepingDbContextModelSnapshot
- CurrentUserResponse
- AuthenticationRequests
- PaymentCard
- IDocumentExtractionAdapter
- .Create()
- BankAccount
- OutboxMessage
- auth-api
- payments-api
- ClaimsPrincipal
- PaymentEndpoints
- Contapop.Billing.Service/Api/ReplicationEndpoints
- CreateInvoiceLine
- FinancialRecord
- .MapProjectReplicationEndpoints()
- TransactionReplica
- ReplicationConsumerIntegrationTests
- TransactionReplica
- BookkeepingDbContext
- .Create()
- User
- ReconciliationDbContext
- Contapop.Reconciliation.Service/Properties/launchSettin
- 20260909054920 InitialBillingSchema
- ProjectReplicationConsumerIntegrationTests
- .Retry()
- dependencies
- Contapop.Application.AppHost/Properties/launchSettings.
- Counterparty
- .ConfigureFinancialRecord()
- Plan
- 20260909065516 AddCounterpartyIdempotencyRecords
- 20260909072337 AddInvoiceCommands
- 20260912134252 AddInvoiceAttachments
- ProvisionTenantCommand
- Tenant
- FixedClock
- 20260908062538 AddAccountCommands
- 20260908062932 AddIdempotencyRecords
- 20260908175157 AddReconciliationClaims
- ReconciliationDbContext
- Contapop.Application.Serverproj
- 20260909184458 AddInvoiceLines
- ProjectReplica
- FinancialRecordCommandHandler
- OutboxMessage
- PlannedLine
- Contapop.Experience.Apiproj
- 20260909055056 AddOutboxDispatchState
- ProjectReplica
- LedgerSchemaMigrationTests
- .GetAsync()
- InvoiceCommands
- 20260912122551 AddArchivedInvoiceStatus
- 20260912163056 AddInvoiceAttachmentCollection
- BillingDbContextModelSnapshot
- FinancialRecordEndpoints
- PlanVsActualCalculator
- ProjectReplica
- 20260907211017 AddOutboxDispatchState
- 20260908073544 AddTransactionDescriptions
- updateDraftInvoice()
- Contapop.Application.Server/Properties/launchSettings.j
- InvoiceLine
- IdentitySchemaMigrationTests
- 20260907110906 InitialLedgerSchema
- Contapop.Application.slnx
- Extensions
- ReplicationEndpoints
- Contapop.Billing.Serviceproj
- ReplicationEndpoints
- Contapop.Identity.Serviceproj
- Contapop.Identity.Service.Testsproj
- Contapop.Ledger.Serviceproj
- TransactionEvents
- InboxMessage
- Contapop.Reconciliation.Serviceproj
- Contapop.Application.AppHost
- Contapop.Bookkeeping.Serviceproj
- Persistence/InboxMessage
- Contapop.Experience.Api.Testsproj
- Contapop.Ledger.Service.Testsproj
- scripts
- LedgerReconciliationClaimValidator
- Contapop.Billing.Service.Testsproj
- LedgerReconciliationClaimValidator
- AccountCommands
- .Create()
- DomainEventOutboxInterceptor
- LedgerDbContextModelSnapshot
- phase-2.spec
- package.json
- PaymentEndpoints
- Contapop.Bookkeeping.Service/Application/Abstractions/I
- brace-expansion
- PaymentCommands
- Contapop.Billing.Service/Application/Replication/Integr
- CounterpartyTests
- .BuildTargetModel()
- Contapop.Application.Server/Program
- .IsValidAsync()
- BankAccountLinked
- DependentResult
- Aspire Starter Title
- tsconfig.json
- .Request()
- eslint-plugin-react-refresh
- phase-1.spec
- globals
- jsdom
- @playwright/test
- @testing-library/react
- @types/react-dom
- @vitejs/plugin-react
- vitest
- GitHub Logo

## God Nodes (most connected - your core abstractions)
1. `LedgerDbContext` - 61 edges
2. `BillingDbContext` - 56 edges
3. `BookkeepingDbContext` - 47 edges
4. `ReconciliationOperation` - 34 edges
5. `IdentityDbContext` - 33 edges
6. `apiRequest()` - 32 edges
7. `Invoice` - 31 edges
8. `FinancialRecordCommandHandler` - 31 edges
9. `OutboxMessage` - 31 edges
10. `RecordResult` - 29 edges

## Surprising Connections (you probably didn't know these)
- `ExperienceUserIntegrationTests` --references--> `Program`  [EXTRACTED]
  Contapop.Experience.Api.Tests/ExperienceUserIntegrationTests.cs → Contapop.Billing.Service/Program.cs
- `AuthenticationIntegrationTests` --references--> `Program`  [EXTRACTED]
  Contapop.Identity.Service.Tests/Integration/AuthenticationIntegrationTests.cs → Contapop.Billing.Service/Program.cs
- `ClaimValidator` --implements--> `IReconciliationClaimValidator`  [EXTRACTED]
  Contapop.Bookkeeping.Service.Tests/Integration/DocumentImportIntegrationTests.cs → Contapop.Bookkeeping.Service/Application/Abstractions/IReconciliationClaimValidator.cs
- `ClaimValidator` --implements--> `IReconciliationClaimValidator`  [EXTRACTED]
  Contapop.Bookkeeping.Service.Tests/Integration/FinancialRecordCommandIntegrationTests.cs → Contapop.Bookkeeping.Service/Application/Abstractions/IReconciliationClaimValidator.cs
- `ExtractionAdapter` --implements--> `IDocumentExtractionAdapter`  [EXTRACTED]
  Contapop.Bookkeeping.Service.Tests/Integration/FinancialRecordCommandIntegrationTests.cs → Contapop.Bookkeeping.Service/Application/Abstractions/IDocumentExtractionAdapter.cs

## Import Cycles
- None detected.

## Communities (173 total, 14 thin omitted)

### Community 0 - "TransactionEndpoints"
Cohesion: 0.06
Nodes (58): ImportTransactionsResponse, RecordTransactionRequest, TransactionDetailsResponse, TransactionEndpoints, TransactionListItem, UpdateTransactionRequest, CancellationToken, DateOnly (+50 more)

### Community 1 - "BlobServiceClient"
Cohesion: 0.08
Nodes (45): BlobServiceClient, CreateInvoiceLineRequest, CreateInvoiceRequest, InvoiceDetailsResponse, Attachments, Lines, Payments, InvoiceEndpoints (+37 more)

### Community 2 - "PlanEndpoints"
Cohesion: 0.08
Nodes (39): AddPlannedLineRequest, CreatePlanRequest, PlanDetailResponse, PlanEndpoints, PlanListItem, PlannedLineDetail, PlanPagedResponse, PlanPeriod (+31 more)

### Community 3 - "ReconciliationClaimEndpoints"
Cohesion: 0.06
Nodes (41): ReconciliationClaimEndpoints, ReserveClaimRequest, ValidateClaimRequest, CancellationToken, Guid, HttpContext, IEndpointRouteBuilder, IResult (+33 more)

### Community 4 - "CounterpartyEndpoints"
Cohesion: 0.08
Nodes (33): CounterpartyEndpoints, CounterpartyListItem, CounterpartyPagedResponse, CreateCounterpartyRequest, UpdateCounterpartyRequest, CancellationToken, Dictionary, Guid (+25 more)

### Community 5 - "Amount"
Cohesion: 0.10
Nodes (26): Amount, SkippedImportRow, TransactionColumnMapping, TransactionFileImporter, TransactionFileImportException, SkippedRows, TransactionFileImportParseResult, CancellationToken (+18 more)

### Community 6 - ".OnModelCreating()"
Cohesion: 0.05
Nodes (42): ModelBuilder, AttachmentCleanup, AttemptCount, BlobName, CreatedAt, Id, NextAttemptAt, TenantId (+34 more)

### Community 7 - "invoices-api"
Cohesion: 0.10
Nodes (35): changeInvoiceStatus(), createCounterparty(), createInvoice(), deleteDraftInvoice(), downloadInvoiceAttachment(), getCounterparties(), getInvoice(), getInvoices() (+27 more)

### Community 8 - "20260912205349 InitialBookkeepingReplication"
Cohesion: 0.05
Nodes (28): DateOnly, DateTimeOffset, Guid, MigrationBuilder, InitialBookkeepingReplication, DateOnly, DateTimeOffset, Guid (+20 more)

### Community 9 - ".Retry()"
Cohesion: 0.06
Nodes (32): Invoice, CounterpartyId, CreatedAt, Date, Direction, DueDate, Id, Lines (+24 more)

### Community 10 - "IReconciliationClaimValidator"
Cohesion: 0.15
Nodes (16): IReconciliationClaimValidator, InvoiceCommandHandler, AcceptClaim, FixedTimeProvider, InvoiceCommandHandlerIntegrationTests, RejectClaim, CancellationToken, DateTimeOffset (+8 more)

### Community 11 - "Appx"
Cohesion: 0.09
Nodes (24): App(), CurrentUser, getCurrentUser(), updateUserPreferences(), updateUserProfile(), authKeys, currentUserOptions, useCurrentUser() (+16 more)

### Community 12 - "IDomainEvent"
Cohesion: 0.07
Nodes (27): IDomainEvent, ProjectCreated, DateTimeOffset, Guid, TenantProvisioned, DateTimeOffset, Guid, Project (+19 more)

### Community 13 - "FinancialRecordCommandHandler"
Cohesion: 0.21
Nodes (9): FinancialRecordCommandHandler, FinancialRecordResponse, RecordResult, Error, Value, CancellationToken, Task, Expense (+1 more)

### Community 14 - "IClock"
Cohesion: 0.09
Nodes (22): IClock, UtcNow, DateTimeOffset, UpdateUserPreferencesCommandHandler, UpdateUserProfileCommandHandler, IdentityDbContextFactory, SystemClock, UtcNow (+14 more)

### Community 15 - "ProjectReplicationEndpoints"
Cohesion: 0.09
Nodes (18): ProjectReplicationEndpoints, PaymentCardLinked, DateTimeOffset, Guid, LedgerDbContextFactory, Program, Contapop.Ledger.Service.Infrastructure.Messaging, Contapop.Ledger.Service.Infrastructure.Replication (+10 more)

### Community 16 - "useBankAccounts()"
Cohesion: 0.12
Nodes (29): useBankAccounts(), archiveTransaction(), createTransaction(), getTransaction(), getTransactions(), importTransactions(), transactionParameters(), updateTransaction() (+21 more)

### Community 17 - "AccountEndpoints"
Cohesion: 0.19
Nodes (17): AccountEndpoints, AddPaymentCardLabelRequest, BankAccountListItem, LinkBankAccountRequest, PagedResponse, PaymentCardListItem, CancellationToken, DateOnly (+9 more)

### Community 18 - "BackgroundService"
Cohesion: 0.08
Nodes (24): BackgroundService, InvoiceAttachmentCleanupHostedService, CancellationToken, ILogger, IServiceScopeFactory, Task, MarkInvoicesOverdueHostedService, MarkInvoicesOverdueJob (+16 more)

### Community 19 - "20260906133029 InitialIdentitySchema"
Cohesion: 0.08
Nodes (18): DateTimeOffset, Guid, MigrationBuilder, InitialIdentitySchema, DateTimeOffset, Guid, ModelBuilder, DateTimeOffset (+10 more)

### Community 20 - "ProvisionTenantCommand"
Cohesion: 0.12
Nodes (20): ProvisionTenantCommand, ProvisionTenantCommandHandler, CancellationToken, Task, FailingPublisher, FixedClock, UtcNow, OutboxDispatcherIntegrationTests (+12 more)

### Community 21 - "InvoiceCommandHandler"
Cohesion: 0.19
Nodes (13): CreatedInvoiceResponse, DeletedInvoiceResponse, InvoiceCommandResult, IsConflict, IsCounterpartyUnavailable, IsNotFound, IsProjectUnavailable, Value (+5 more)

### Community 22 - "accounts-api"
Cohesion: 0.13
Nodes (22): archiveBankAccount(), createBankAccount(), createPaymentCard(), getBankAccounts(), getPaymentCards(), removePaymentCard(), accountKeys, bankAccountsOptions (+14 more)

### Community 23 - "Contapop.Bookkeeping.Service/Api/ReplicationEndpoints"
Cohesion: 0.13
Nodes (12): ProjectReplicationConsumer, TransactionReplicationConsumer, Program, Contapop.Bookkeeping.Service.Infrastructure.DocumentExtraction, Contapop.Bookkeeping.Service.Infrastructure.Replication, Contapop.Bookkeeping.Service.Application.Commands, Contapop.Bookkeeping.Service.Api, Contapop.Bookkeeping.Service.Infrastructure.Persistence (+4 more)

### Community 24 - "LedgerDbContext"
Cohesion: 0.15
Nodes (18): LedgerDbContext, BankAccounts, IdempotencyRecords, InboxMessages, OutboxMessages, PaymentCards, ProjectReplicas, ReconciliationClaims (+10 more)

### Community 25 - "Contapop.Billing.Service/Application/Replication/Projec"
Cohesion: 0.09
Nodes (19): ProjectReplicationConsumer, TransactionReplicationConsumer, BillingDbContext, AttachmentCleanups, Counterparties, IdempotencyRecords, InboxMessages, InvoiceAttachments (+11 more)

### Community 26 - "AccountCommandHandler"
Cohesion: 0.20
Nodes (14): AccountCommandHandler, ArchivedBankAccountResult, CommandResult, IsConflict, IsNotFound, IsProjectUnavailable, Succeeded, Value (+6 more)

### Community 27 - "tsconfig.app.json"
Cohesion: 0.08
Nodes (24): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, jsx, lib, module, moduleDetection, moduleResolution (+16 more)

### Community 28 - "EventPayload"
Cohesion: 0.21
Nodes (10): EventPayload, DateOnly, DateTimeOffset, Guid, InvalidOperationException, JsonElement, CancellationToken, Task (+2 more)

### Community 29 - "DocumentExtractionResult"
Cohesion: 0.15
Nodes (15): DocumentExtractionResult, IDocumentExtractionAdapter, CancellationToken, DateOnly, ReadOnlyMemory, Task, ClaimValidator, DocumentImportIntegrationTests (+7 more)

### Community 30 - "EventPayload"
Cohesion: 0.22
Nodes (10): EventPayload, DateOnly, DateTimeOffset, Guid, InvalidOperationException, JsonElement, CancellationToken, Task (+2 more)

### Community 31 - "Contapop.Ledger.Service/Application/Abstractions/IInteg"
Cohesion: 0.12
Nodes (15): IIntegrationEventPublisher, CancellationToken, Task, DaprIntegrationEventPublisher, CancellationToken, DaprClient, JsonElement, Task (+7 more)

### Community 32 - "20260908175710 InitialReconciliationSchema"
Cohesion: 0.10
Nodes (13): DateTimeOffset, Guid, MigrationBuilder, InitialReconciliationSchema, DateTimeOffset, Guid, ModelBuilder, MigrationBuilder (+5 more)

### Community 33 - "ReconciliationOperation"
Cohesion: 0.11
Nodes (18): ReconciliationOperation, Attempts, ClaimId, ClaimVersion, CreatedAt, DependentId, DependentType, ExpectedDependentVersion (+10 more)

### Community 34 - "Contapop.Identity.Service/Application/Abstractions/IInt"
Cohesion: 0.11
Nodes (15): IIntegrationEventPublisher, CancellationToken, Task, DaprIntegrationEventPublisher, CancellationToken, DaprClient, JsonElement, Task (+7 more)

### Community 35 - "OutboxMessage"
Cohesion: 0.11
Nodes (19): OutboxMessage, ActorId, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId (+11 more)

### Community 36 - "Transaction"
Cohesion: 0.11
Nodes (18): Transaction, AmountMinor, BankAccountId, CreatedAt, Date, Description, DomainEvents, Id (+10 more)

### Community 37 - "IntegrationEventEnvelope"
Cohesion: 0.23
Nodes (11): IntegrationEventEnvelope, DateTimeOffset, Guid, JsonElement, ReplicationConsumerIntegrationTests, DateTimeOffset, DbContextOptions, Fact (+3 more)

### Community 38 - "ExperienceUserIntegrationTests"
Cohesion: 0.23
Nodes (8): ExperienceUserIntegrationTests, Fact, InlineData, Task, Theory, WebApplication, Contapop.Experience.Api.Tests, HttpRequestMessage

### Community 39 - "UpdateUserPreferencesCommand"
Cohesion: 0.14
Nodes (13): UpdateUserPreferencesCommand, UpdateUserPreferencesResult, DateTimeOffset, Guid, CancellationToken, Task, UpdateUserPreferencesCommandValidator, Dictionary (+5 more)

### Community 40 - "IdentityCredential"
Cohesion: 0.10
Nodes (20): IdentityCredential, DomainUserId, TenantId, Guid, IdentityDbContext, DomainUsers, OutboxMessages, Projects (+12 more)

### Community 41 - "eslint"
Cohesion: 0.10
Nodes (21): eslint, @eslint/js, eslint-plugin-react-hooks, devDependencies, eslint, @eslint/js, eslint-plugin-react-hooks, @testing-library/jest-dom (+13 more)

### Community 42 - "tsconfig.node.json"
Cohesion: 0.10
Nodes (20): compilerOptions, allowImportingTsExtensions, erasableSyntaxOnly, lib, module, moduleDetection, moduleResolution, noEmit (+12 more)

### Community 43 - "PaymentCommandHandler"
Cohesion: 0.21
Nodes (10): PaymentCommandHandler, PaymentCommandResult, Error, Value, PaymentResponse, CancellationToken, DateOnly, DateTimeOffset (+2 more)

### Community 44 - "FinancialRecordEndpoints"
Cohesion: 0.19
Nodes (9): FinancialRecordEndpoints, CancellationToken, DbSet, Func, HttpContext, IEndpointRouteBuilder, IFormFile, IResult (+1 more)

### Community 45 - "BookkeepingDbContextModelSnapshot"
Cohesion: 0.10
Nodes (14): BookkeepingDbContextModelSnapshot, DateOnly, DateTimeOffset, Guid, ModelBuilder, IdentityDbContextModelSnapshot, DateTimeOffset, Guid (+6 more)

### Community 46 - "CurrentUserResponse"
Cohesion: 0.20
Nodes (12): CurrentUserResponse, ProvisionTenantRequest, ProvisionTenantResponse, DateTimeOffset, Guid, AuthenticationIntegrationTests, Fact, Guid (+4 more)

### Community 47 - "AuthenticationRequests"
Cohesion: 0.12
Nodes (14): ChangePasswordRequest, LoginRequest, UpdateUserPreferencesRequest, UpdateUserProfileRequest, UpdateUserProfileCommand, UpdateUserProfileResult, DateTimeOffset, Guid (+6 more)

### Community 48 - "PaymentCard"
Cohesion: 0.12
Nodes (16): PaymentCard, CardholderName, CreatedAt, DomainEvents, ExpirationDate, Id, Label, ProjectId (+8 more)

### Community 49 - "IDocumentExtractionAdapter"
Cohesion: 0.20
Nodes (11): DocumentExtractionException, AzureDocumentExtractionAdapter, CancellationToken, DateOnly, HttpClient, IConfiguration, JsonElement, ReadOnlyMemory (+3 more)

### Community 50 - ".Create()"
Cohesion: 0.26
Nodes (9): ClaimValidator, ExtractionAdapter, FinancialRecordCommandIntegrationTests, CancellationToken, Fact, Guid, PostgreSqlContainer, ReadOnlyMemory (+1 more)

### Community 51 - "BankAccount"
Cohesion: 0.12
Nodes (15): BankAccount, AccountNumber, BankName, CreatedAt, DomainEvents, Id, ProjectId, Status (+7 more)

### Community 52 - "OutboxMessage"
Cohesion: 0.13
Nodes (16): OutboxMessage, AggregateId, AggregateType, AggregateVersion, EventId, EventName, LastError, LockedUntil (+8 more)

### Community 53 - "auth-api"
Cohesion: 0.16
Nodes (13): login(), LoginInput, UpdateUserPreferencesInput, UpdateUserPreferencesResponse, UpdateUserProfileInput, UpdateUserProfileResponse, LoginFormValues, LoginLocationState (+5 more)

### Community 54 - "payments-api"
Cohesion: 0.20
Nodes (14): getPayments(), getUnreconciledTransactions(), Payment, reconcilePayment(), recordPayment(), Transaction, paymentKeys, usePayments() (+6 more)

### Community 55 - "ClaimsPrincipal"
Cohesion: 0.14
Nodes (15): ClaimsPrincipal, AuthenticatedUserResponse, DateTimeOffset, Guid, IConfiguration, InternalJwtIssuer, LoginRequest, Program (+7 more)

### Community 56 - "PaymentEndpoints"
Cohesion: 0.29
Nodes (8): PaymentEndpoints, ReconcilePaymentRequest, CancellationToken, Guid, HttpContext, IEndpointRouteBuilder, IResult, Task

### Community 57 - "Contapop.Billing.Service/Api/ReplicationEndpoints"
Cohesion: 0.15
Nodes (9): Program, Contapop.Billing.Service.Application.Attachments, Contapop.Billing.Service.Application.BackgroundJobs, Contapop.Billing.Service.Application.Abstractions, Contapop.Billing.Service.Infrastructure.Reconciliation, Contapop.Billing.Service.Application.Commands, Contapop.Billing.Service.Application.Replication, Contapop.Billing.Service.Tests.Integration (+1 more)

### Community 58 - "CreateInvoiceLine"
Cohesion: 0.24
Nodes (6): CreateInvoiceLine, IReadOnlyList, InvoiceTests, Fact, InlineData, Theory

### Community 59 - "FinancialRecord"
Cohesion: 0.12
Nodes (15): FinancialRecord, AmountMinor, Category, ConfirmedAt, CreatedAt, Date, Id, ImportSource (+7 more)

### Community 60 - ".MapProjectReplicationEndpoints()"
Cohesion: 0.18
Nodes (11): IEndpointRouteBuilder, IntegrationEventEnvelope, DateTimeOffset, Guid, JsonElement, ProjectReplicationConsumer, CancellationToken, DateTimeOffset (+3 more)

### Community 61 - "TransactionReplica"
Cohesion: 0.16
Nodes (15): TransactionReplica, AggregateVersion, AmountMinor, BankAccountId, CreatedAt, Date, Description, Status (+7 more)

### Community 62 - "ReplicationConsumerIntegrationTests"
Cohesion: 0.31
Nodes (7): ReplicationConsumerIntegrationTests, DateTimeOffset, DbContextOptions, Fact, Guid, PostgreSqlContainer, Task

### Community 63 - "TransactionReplica"
Cohesion: 0.16
Nodes (15): TransactionReplica, AggregateVersion, AmountMinor, BankAccountId, CreatedAt, Date, Description, Status (+7 more)

### Community 64 - "BookkeepingDbContext"
Cohesion: 0.13
Nodes (14): BookkeepingDbContext, Expenses, IdempotencyRecords, InboxMessages, OutboxMessages, PlannedExpenses, PlannedRevenues, Plans (+6 more)

### Community 65 - ".Create()"
Cohesion: 0.28
Nodes (3): DateOnly, DateTimeOffset, Guid

### Community 66 - "User"
Cohesion: 0.16
Nodes (13): User, CreatedAt, Email, Id, Language, Name, NotificationsEnabled, TenantId (+5 more)

### Community 67 - "ReconciliationDbContext"
Cohesion: 0.15
Nodes (8): Program, InternalJwtIssuer, IConfiguration, PaymentReconciliationEndpoints, IEndpointRouteBuilder, Contapop.Reconciliation.Service, Contapop.Reconciliation.Service.Reconciliation, Contapop.Reconciliation.Service.Infrastructure.Persistence

### Community 68 - "Contapop.Reconciliation.Service/Properties/launchSettin"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 69 - "20260909054920 InitialBillingSchema"
Cohesion: 0.15
Nodes (10): DateOnly, DateTimeOffset, Guid, MigrationBuilder, InitialBillingSchema, DateOnly, DateTimeOffset, Guid (+2 more)

### Community 70 - "ProjectReplicationConsumerIntegrationTests"
Cohesion: 0.30
Nodes (7): ProjectReplicationConsumerIntegrationTests, DateTimeOffset, DbContextOptions, Fact, Guid, PostgreSqlContainer, Task

### Community 71 - ".Retry()"
Cohesion: 0.30
Nodes (8): PaymentReconciliationCoordinator, CancellationToken, ILogger, Task, DependentResult, IHttpClientFactory, InternalJwtIssuer, ReservationResponse

### Community 72 - "dependencies"
Cohesion: 0.13
Nodes (15): dependencies, @hookform/resolvers, react, react-dom, react-hook-form, react-router-dom, @tanstack/react-query, zod (+7 more)

### Community 73 - "Contapop.Application.AppHost/Properties/launchSettings."
Cohesion: 0.14
Nodes (13): ASPIRE_ALLOW_UNSECURED_TRANSPORT, ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL, ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages (+5 more)

### Community 74 - "Counterparty"
Cohesion: 0.14
Nodes (12): Counterparty, Address, CreatedAt, Email, Id, Name, Status, TaxId (+4 more)

### Community 75 - ".ConfigureFinancialRecord()"
Cohesion: 0.18
Nodes (10): ModelBuilder, IdempotencyRecord, CreatedAt, Key, Operation, Result, TenantId, PlannedExpense (+2 more)

### Community 76 - "Plan"
Cohesion: 0.14
Nodes (13): Plan, AllocatedAmountMinor, CreatedAt, Description, EndDate, Id, ProjectId, StartDate (+5 more)

### Community 77 - "20260909065516 AddCounterpartyIdempotencyRecords"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddCounterpartyIdempotencyRecords, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 78 - "20260909072337 AddInvoiceCommands"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddInvoiceCommands, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 79 - "20260912134252 AddInvoiceAttachments"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddInvoiceAttachments, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 80 - "ProvisionTenantCommand"
Cohesion: 0.17
Nodes (8): ProvisionTenantResult, DateTimeOffset, Guid, ProvisionTenantCommandValidator, Dictionary, ProvisionTenantCommandValidatorTests, Fact, Contapop.Identity.Service.Application.Commands.ProvisionTenant

### Community 81 - "Tenant"
Cohesion: 0.19
Nodes (10): Tenant, CreatedAt, DomainEvents, Id, Name, UpdatedAt, DateTimeOffset, Guid (+2 more)

### Community 82 - "FixedClock"
Cohesion: 0.27
Nodes (8): FixedClock, UtcNow, ProvisionTenantIntegrationTests, DateTimeOffset, DbContextOptions, Fact, PostgreSqlContainer, Task

### Community 83 - "20260908062538 AddAccountCommands"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddAccountCommands, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 84 - "20260908062932 AddIdempotencyRecords"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddIdempotencyRecords, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 85 - "20260908175157 AddReconciliationClaims"
Cohesion: 0.17
Nodes (8): DateTimeOffset, Guid, MigrationBuilder, AddReconciliationClaims, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 86 - "ReconciliationDbContext"
Cohesion: 0.17
Nodes (10): ReconciliationDbContext, Operations, DbContextOptions, DbSet, ModelBuilder, ReconciliationDbContextFactory, CancellationToken, PaymentReconciliationCoordinator (+2 more)

### Community 87 - "Contapop.Application.Serverproj"
Cohesion: 0.17
Nodes (11): net10.0, Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.Extensions.Http.Resilience (10.8.0), Microsoft.Extensions.ServiceDiscovery (10.8.0), OpenTelemetry.Exporter.OpenTelemetryProtocol (1.15.3), OpenTelemetry.Extensions.Hosting (1.15.3), OpenTelemetry.Instrumentation.AspNetCore (1.15.2), OpenTelemetry.Instrumentation.Http (1.15.1) (+3 more)

### Community 88 - "20260909184458 AddInvoiceLines"
Cohesion: 0.18
Nodes (7): Guid, MigrationBuilder, AddInvoiceLines, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 89 - "ProjectReplica"
Cohesion: 0.21
Nodes (10): ProjectReplica, AggregateVersion, CreatedAt, Name, ProjectId, Status, TenantId, UpdatedAt (+2 more)

### Community 90 - "FinancialRecordCommandHandler"
Cohesion: 0.32
Nodes (11): ConfirmImportedFinancialRecordCommand, CreateFinancialRecordCommand, DeletedFinancialRecordResponse, DeleteFinancialRecordCommand, ImportedFinancialRecordResponse, ImportFinancialRecordCommand, ReconcileFinancialRecordCommand, UpdateFinancialRecordCommand (+3 more)

### Community 91 - "OutboxMessage"
Cohesion: 0.17
Nodes (12): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventName (+4 more)

### Community 92 - "PlannedLine"
Cohesion: 0.17
Nodes (12): PlannedLine, AmountMinor, Category, CreatedAt, Date, Id, PlanId, ProjectId (+4 more)

### Community 93 - "Contapop.Experience.Apiproj"
Cohesion: 0.17
Nodes (11): net10.0, Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.Extensions.Http.Resilience (10.8.0), Microsoft.Extensions.ServiceDiscovery (10.8.0), OpenTelemetry.Exporter.OpenTelemetryProtocol (1.15.3), OpenTelemetry.Extensions.Hosting (1.15.3), OpenTelemetry.Instrumentation.AspNetCore (1.15.2), OpenTelemetry.Instrumentation.Http (1.15.1) (+3 more)

### Community 94 - "20260909055056 AddOutboxDispatchState"
Cohesion: 0.18
Nodes (7): DateTimeOffset, MigrationBuilder, AddOutboxDispatchState, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 95 - "ProjectReplica"
Cohesion: 0.21
Nodes (10): ProjectReplica, AggregateVersion, CreatedAt, Name, ProjectId, Status, TenantId, UpdatedAt (+2 more)

### Community 96 - "LedgerSchemaMigrationTests"
Cohesion: 0.24
Nodes (6): LedgerSchemaMigrationTests, DbContextOptions, Fact, List, PostgreSqlContainer, Task

### Community 97 - ".GetAsync()"
Cohesion: 0.21
Nodes (9): ReservationResponse, StartPaymentReconciliationRequest, Guid, PaymentReconciliationResponse, CancellationToken, Guid, HttpContext, IResult (+1 more)

### Community 98 - "InvoiceCommands"
Cohesion: 0.35
Nodes (10): ArchiveInvoiceCommand, CreateInvoiceCommand, CreateInvoiceLineCommand, DeleteDraftInvoiceCommand, IssueInvoiceCommand, UpdateDraftInvoiceCommand, VoidInvoiceCommand, DateOnly (+2 more)

### Community 99 - "20260912122551 AddArchivedInvoiceStatus"
Cohesion: 0.20
Nodes (6): MigrationBuilder, AddArchivedInvoiceStatus, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 100 - "20260912163056 AddInvoiceAttachmentCollection"
Cohesion: 0.20
Nodes (6): MigrationBuilder, AddInvoiceAttachmentCollection, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 101 - "BillingDbContextModelSnapshot"
Cohesion: 0.20
Nodes (7): BillingDbContextModelSnapshot, DateOnly, DateTimeOffset, Guid, ModelBuilder, Contapop.Billing.Service.Infrastructure.Persistence, Contapop.Billing.Service.Tests.Unit

### Community 102 - "FinancialRecordEndpoints"
Cohesion: 0.29
Nodes (10): ConfirmImportedFinancialRecordRequest, CreateFinancialRecordRequest, FinancialRecordListItem, FinancialRecordPagedResponse, ReconcileFinancialRecordRequest, UpdateFinancialRecordRequest, DateOnly, DateTimeOffset (+2 more)

### Community 103 - "PlanVsActualCalculator"
Cohesion: 0.31
Nodes (8): PlanVsActualCalculator, PlanVsActualCategory, PlanVsActualData, PlanVsActualTotals, VarianceMinor, IReadOnlyList, Contapop.Bookkeeping.Service.Application.Queries, IEnumerable

### Community 104 - "ProjectReplica"
Cohesion: 0.20
Nodes (10): ProjectReplica, AggregateVersion, CreatedAt, Name, ProjectId, Status, TenantId, UpdatedAt (+2 more)

### Community 105 - "20260907211017 AddOutboxDispatchState"
Cohesion: 0.20
Nodes (6): DateTimeOffset, MigrationBuilder, AddOutboxDispatchState, DateTimeOffset, Guid, ModelBuilder

### Community 106 - "20260908073544 AddTransactionDescriptions"
Cohesion: 0.20
Nodes (6): MigrationBuilder, AddTransactionDescriptions, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 107 - "updateDraftInvoice()"
Cohesion: 0.24
Nodes (8): updateDraftInvoice(), invoiceKeys, EditInvoicePage(), lineSchema, money(), schema, mocks, Values

### Community 108 - "Contapop.Application.Server/Properties/launchSettings.j"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, http (+1 more)

### Community 109 - "InvoiceLine"
Cohesion: 0.20
Nodes (10): InvoiceLine, Description, Id, InvoiceId, NetAmountMinor, Quantity, TaxAmountMinor, TaxRate (+2 more)

### Community 110 - "IdentitySchemaMigrationTests"
Cohesion: 0.29
Nodes (5): IdentitySchemaMigrationTests, Fact, List, PostgreSqlContainer, Task

### Community 111 - "20260907110906 InitialLedgerSchema"
Cohesion: 0.24
Nodes (6): DateOnly, DateTimeOffset, Guid, MigrationBuilder, InitialLedgerSchema, Contapop.Ledger.Service.Infrastructure.Persistence.Migrations

### Community 112 - "Contapop.Application.slnx"
Cohesion: 0.22
Nodes (7): net10.0, Microsoft.NET.Test.Sdk (18.3.0), Testcontainers.PostgreSql (4.14.0), xunit (2.9.3), xunit.runner.visualstudio (2.8.2), Microsoft.NET.Sdk, frontend

### Community 113 - "Extensions"
Cohesion: 0.22
Nodes (3): WebApplication, Extensions, Microsoft.Extensions.Hosting

### Community 114 - "ReplicationEndpoints"
Cohesion: 0.25
Nodes (7): ReplicationEndpoints, CancellationToken, Func, IEndpointRouteBuilder, IResult, JsonElement, Task

### Community 115 - "Contapop.Billing.Serviceproj"
Cohesion: 0.22
Nodes (8): net10.0, Dapr.AspNetCore (1.15.4), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web, Azure.Storage.Blobs (12.26.0)

### Community 116 - "ReplicationEndpoints"
Cohesion: 0.25
Nodes (7): ReplicationEndpoints, CancellationToken, Func, IEndpointRouteBuilder, IResult, JsonElement, Task

### Community 117 - "Contapop.Identity.Serviceproj"
Cohesion: 0.22
Nodes (8): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web, Dapr.Client (1.15.4), Microsoft.AspNetCore.Identity.EntityFrameworkCore (9.0.0)

### Community 118 - "Contapop.Identity.Service.Testsproj"
Cohesion: 0.22
Nodes (8): net10.0, Microsoft.AspNetCore.Mvc.Testing (10.0.11), Microsoft.NET.Test.Sdk (18.3.0), System.IdentityModel.Tokens.Jwt (8.19.2), Testcontainers.PostgreSql (4.14.0), xunit (2.9.3), xunit.runner.visualstudio (2.8.2), Microsoft.NET.Sdk

### Community 119 - "Contapop.Ledger.Serviceproj"
Cohesion: 0.22
Nodes (8): net10.0, Dapr.AspNetCore (1.15.4), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web, ClosedXML (0.105.0)

### Community 120 - "TransactionEvents"
Cohesion: 0.42
Nodes (7): TransactionArchived, TransactionRecorded, TransactionUpdated, DateOnly, DateTimeOffset, Guid, DbContext

### Community 121 - "InboxMessage"
Cohesion: 0.28
Nodes (7): InboxMessage, ConsumerName, EventId, ProcessedAt, DateTimeOffset, Guid, ModelBuilder

### Community 122 - "Contapop.Reconciliation.Serviceproj"
Cohesion: 0.22
Nodes (8): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Microsoft.Extensions.Http.Resilience (10.8.0), Microsoft.Extensions.ServiceDiscovery (10.8.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), System.IdentityModel.Tokens.Jwt (8.19.2), Microsoft.NET.Sdk.Web

### Community 123 - "Contapop.Application.AppHost"
Cohesion: 0.25
Nodes (8): Contapop.Application.AppHost, net10.0, Aspire.Hosting.Azure.Storage (13.5.3), Aspire.Hosting.JavaScript (13.5.3), Aspire.Hosting.PostgreSQL (13.5.3), Aspire.Hosting.Redis (13.5.3), CommunityToolkit.Aspire.Hosting.Dapr (13.5.1-beta.748), Aspire.AppHost.Sdk/13.5.3

### Community 124 - "Contapop.Bookkeeping.Serviceproj"
Cohesion: 0.25
Nodes (7): net10.0, Dapr.AspNetCore (1.15.4), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.11), Microsoft.EntityFrameworkCore.Design (9.0.0), Npgsql.EntityFrameworkCore.PostgreSQL (9.0.0), Microsoft.NET.Sdk.Web

### Community 125 - "Persistence/InboxMessage"
Cohesion: 0.32
Nodes (6): InboxMessage, ConsumerName, EventId, ProcessedAt, DateTimeOffset, Guid

### Community 126 - "Contapop.Experience.Api.Testsproj"
Cohesion: 0.25
Nodes (7): net10.0, Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.Mvc.Testing (10.0.11), Microsoft.NET.Test.Sdk (18.3.0), xunit (2.9.3), xunit.runner.visualstudio (2.8.2), Microsoft.NET.Sdk

### Community 127 - "Contapop.Ledger.Service.Testsproj"
Cohesion: 0.25
Nodes (7): net10.0, Microsoft.NET.Test.Sdk (18.3.0), Testcontainers.PostgreSql (4.14.0), xunit (2.9.3), xunit.runner.visualstudio (2.8.2), Microsoft.NET.Sdk, Microsoft.AspNetCore.TestHost (10.0.11)

### Community 128 - "scripts"
Cohesion: 0.25
Nodes (8): scripts, build, dev, lint, preview, test, test:e2e, test:e2e:ui

### Community 129 - "LedgerReconciliationClaimValidator"
Cohesion: 0.29
Nodes (6): LedgerReconciliationClaimValidator, CancellationToken, Guid, HttpClient, IHttpContextAccessor, Task

### Community 130 - "Contapop.Billing.Service.Testsproj"
Cohesion: 0.29
Nodes (6): net10.0, Microsoft.NET.Test.Sdk (18.3.0), Testcontainers.PostgreSql (4.14.0), xunit (2.9.3), xunit.runner.visualstudio (2.8.2), Microsoft.NET.Sdk

### Community 131 - "LedgerReconciliationClaimValidator"
Cohesion: 0.29
Nodes (6): LedgerReconciliationClaimValidator, CancellationToken, Guid, HttpClient, IHttpContextAccessor, Task

### Community 132 - "AccountCommands"
Cohesion: 0.43
Nodes (6): AddPaymentCardLabelCommand, ArchiveBankAccountCommand, LinkBankAccountCommand, RemovePaymentCardLabelCommand, DateOnly, Guid

### Community 133 - ".Create()"
Cohesion: 0.38
Nodes (3): TransactionTests, Fact, Contapop.Ledger.Service.Tests.Unit

### Community 134 - "DomainEventOutboxInterceptor"
Cohesion: 0.29
Nodes (6): DomainEventOutboxInterceptor, CancellationToken, DbContextEventData, InterceptionResult, ValueTask, SaveChangesInterceptor

### Community 135 - "LedgerDbContextModelSnapshot"
Cohesion: 0.29
Nodes (5): LedgerDbContextModelSnapshot, DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 137 - "package.json"
Cohesion: 0.29
Nodes (6): engines, node, name, private, type, version

### Community 138 - "PaymentEndpoints"
Cohesion: 0.47
Nodes (5): PaymentListItem, PaymentPagedResponse, RecordPaymentRequest, DateOnly, IReadOnlyList

### Community 139 - "Contapop.Bookkeeping.Service/Application/Abstractions/I"
Cohesion: 0.33
Nodes (4): IReconciliationClaimValidator, CancellationToken, Guid, Task

### Community 140 - "brace-expansion"
Cohesion: 0.33
Nodes (6): brace-expansion, overrides, esbuild, js-yaml, minimatch@3.1.5, postcss

### Community 141 - "PaymentCommands"
Cohesion: 0.50
Nodes (4): ReconcilePaymentCommand, RecordPaymentCommand, DateOnly, Guid

### Community 142 - "Contapop.Billing.Service/Application/Replication/Integr"
Cohesion: 0.40
Nodes (4): IntegrationEventEnvelope, DateTimeOffset, Guid, JsonElement

### Community 144 - ".BuildTargetModel()"
Cohesion: 0.40
Nodes (4): DateOnly, DateTimeOffset, Guid, ModelBuilder

### Community 145 - "Contapop.Application.Server/Program"
Cohesion: 0.50
Nodes (3): DateOnly, WeatherForecast, TemperatureF

### Community 146 - ".IsValidAsync()"
Cohesion: 0.50
Nodes (3): CancellationToken, Guid, Task

### Community 147 - "BankAccountLinked"
Cohesion: 0.50
Nodes (3): BankAccountLinked, DateTimeOffset, Guid

### Community 148 - "DependentResult"
Cohesion: 0.50
Nodes (4): DependentResult, Failed, Succeeded, Unknown

### Community 149 - "Aspire Starter Title"
Cohesion: 0.67
Nodes (4): Aspire Starter Title, Frontend HTML Document, Main TypeScript Entry Module, React Root Element

## Knowledge Gaps
- **649 isolated node(s):** `net10.0`, `Aspire.Hosting.Azure.Storage (13.5.3)`, `Aspire.Hosting.JavaScript (13.5.3)`, `Aspire.Hosting.PostgreSQL (13.5.3)`, `Aspire.Hosting.Redis (13.5.3)` (+644 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1100 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `BookkeepingDbContext` connect `BookkeepingDbContext` to `PlanEndpoints`, `IntegrationEventEnvelope`, `ProjectReplica`, `.ConfigureFinancialRecord()`, `FinancialRecordEndpoints`, `FinancialRecordCommandHandler`, `Plan`, `DocumentExtractionResult`, `.Create()`, `ReconciliationDbContext`, `Contapop.Bookkeeping.Service/Api/ReplicationEndpoints`, `OutboxMessage`, `Persistence/InboxMessage`, `TransactionReplica`?**
  _High betweenness centrality (0.177) - this node is a cross-community bridge._
- **Why does `BillingDbContext` connect `Contapop.Billing.Service/Application/Replication/Projec` to `BlobServiceClient`, `CounterpartyEndpoints`, `.OnModelCreating()`, `.Retry()`, `IReconciliationClaimValidator`, `PaymentCommandHandler`, `Counterparty`, `InvoiceLine`, `BackgroundService`, `ReconciliationDbContext`, `PaymentEndpoints`, `ProjectReplica`, `TransactionReplica`, `ReplicationConsumerIntegrationTests`?**
  _High betweenness centrality (0.176) - this node is a cross-community bridge._
- **Why does `LedgerDbContext` connect `LedgerDbContext` to `TransactionEndpoints`, `LedgerSchemaMigrationTests`, `ReconciliationClaimEndpoints`, `Transaction`, `Amount`, `ProjectReplicationConsumerIntegrationTests`, `ProjectReplicationEndpoints`, `PaymentCard`, `AccountEndpoints`, `BankAccount`, `OutboxMessage`, `ReconciliationDbContext`, `InboxMessage`, `AccountCommandHandler`, `.MapProjectReplicationEndpoints()`, `ProjectReplica`, `Contapop.Ledger.Service/Application/Abstractions/IInteg`?**
  _High betweenness centrality (0.164) - this node is a cross-community bridge._
- **What connects `net10.0`, `Aspire.Hosting.Azure.Storage (13.5.3)`, `Aspire.Hosting.JavaScript (13.5.3)` to the rest of the system?**
  _649 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `TransactionEndpoints` be split into smaller, more focused modules?**
  _Cohesion score 0.055333473595623815 - nodes in this community are weakly interconnected._
- **Should `BlobServiceClient` be split into smaller, more focused modules?**
  _Cohesion score 0.08290304073436604 - nodes in this community are weakly interconnected._
- **Should `PlanEndpoints` be split into smaller, more focused modules?**
  _Cohesion score 0.0846312077578607 - nodes in this community are weakly interconnected._