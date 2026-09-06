# Backend API Code Guidelines

## Scope And Baseline

These guidelines apply to every ASP.NET Core API service in `src`. The current `Contapop.Application.Server` project is the scaffold; each future System, Process, or Experience API follows these rules within its responsibility.

Use .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core with PostgreSQL, OpenAPI, Problem Details, OpenTelemetry, and xUnit. The API layer exposes HTTP contracts; application code executes use cases; domain code enforces business rules; infrastructure contains EF Core and Dapr integrations.

## Core Rules

- Organize each service by bounded context and use case, not by technical layer across the whole repository.
- Apply CQRS: commands change state; queries read state. A type, handler, or endpoint must not do both.
- Use Entity Framework Core as the ORM and PostgreSQL as the service's system of record. A service never accesses another service's database.
- Keep HTTP request and response DTOs at the API boundary. Explicit extension methods map request DTOs to commands or queries. Do not use AutoMapper.
- Validate commands twice: structural and input validation before dispatch, then current-state and business-invariant validation in the command handler.
- Validate queries before their direct execution. Queries do not use a handler; they must be read-only and cannot trigger domain behavior.
- Use an EF Core interceptor to collect domain changes and persist translated domain-sync events to the transactional outbox. Never publish messages directly from a request, entity method, or interceptor.
- Return RFC 7807 Problem Details for failures. Do not expose exception messages, stack traces, connection strings, or internal topology.
- Use `CancellationToken` for all async endpoint, command, query, repository, and EF Core calls.
- Require an idempotency key and optimistic concurrency token for retriable or update commands.

## Service Structure

Use this structure per independently deployable service. Names below use invoicing as an example.

```text
src/Contapop.Invoicing.Service/
  Api/
    Contracts/
      Requests/
      Responses/
    Endpoints/
    Mapping/
  Application/
    Commands/
      CreateInvoice/
      IssueInvoice/
    Queries/
      GetInvoice/
      ListInvoices/
    Abstractions/
  Domain/
    Invoices/
    Events/
    ValueObjects/
  Infrastructure/
    Persistence/
      InvoicingDbContext.cs
      Configurations/
      Interceptors/
      Outbox/
    Messaging/
  Tests/
    Unit/
```

- `Api` contains endpoint mapping, authorization, request binding, request DTO validation, command/query mapping, and HTTP response mapping.
- `Application` contains commands, command handlers, query execution, application interfaces, and use-case-specific validation.
- `Domain` contains aggregates, value objects, domain events, and invariant-enforcing behavior. It references no EF Core, ASP.NET Core, Dapr, serialization, or HTTP types.
- `Infrastructure` contains EF Core entities/configuration, repositories, interceptors, outbox persistence, Dapr adapters, and external clients.
- `Tests` mirrors the production use-case layout. Test projects must not be deployed with services.

## HTTP Endpoints And DTOs

Minimal API endpoints coordinate HTTP concerns only. They must not contain business rules, EF Core queries, aggregate mutations, or message publication.

```csharp
// Api/Contracts/Requests/CreateInvoiceRequest.cs
public sealed record CreateInvoiceRequest(
    string ClientId,
    DateOnly IssuedOn,
    string Currency,
    IReadOnlyList<CreateInvoiceLineRequest> Lines);

public sealed record CreateInvoiceLineRequest(
    string Description,
    long UnitPriceMinor,
    int Quantity);

// Api/Mapping/InvoiceRequestMappings.cs
public static class InvoiceRequestMappings
{
    public static CreateInvoiceCommand ToCommand(
        this CreateInvoiceRequest request,
        string tenantId,
        string actorId,
        string idempotencyKey) =>
        new(
            tenantId,
            actorId,
            idempotencyKey,
            request.ClientId,
            request.IssuedOn,
            request.Currency,
            request.Lines.Select(line => new CreateInvoiceLineCommand(
                line.Description,
                line.UnitPriceMinor,
                line.Quantity)).ToArray());
}
```

```csharp
// Api/Endpoints/InvoiceEndpoints.cs
public static class InvoiceEndpoints
{
    public static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder api)
    {
        var invoices = api.MapGroup("/invoices").RequireAuthorization("account-owner");

        invoices.MapPost("/", CreateInvoiceAsync)
            .WithName("CreateInvoice")
            .Produces<CreatedInvoiceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return invoices;
    }

    private static async Task<IResult> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        HttpContext httpContext,
        ICurrentUser currentUser,
        ICreateInvoiceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var validation = CreateInvoiceRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.Errors);
        }

        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["An idempotency key is required."],
            });
        }

        var command = request.ToCommand(
            currentUser.TenantId,
            currentUser.UserId,
            idempotencyKey.ToString());
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.Match<IResult>(
            success => Results.Created($"/api/invoices/{success.InvoiceId}",
                new CreatedInvoiceResponse(success.InvoiceId, success.Version)),
            failure => failure.ToProblemDetails());
    }
}
```

- Use request DTOs for inputs and response DTOs for outputs. Never expose EF Core entities or domain aggregates in endpoint signatures or responses.
- Bind only the data required by the operation. Do not accept a broad entity-shaped request and ignore fields.
- Define an extension mapping method close to the endpoint contract. Mapping must be explicit, deterministic, and unit tested for non-trivial cases.
- Extract tenant and actor identity from authenticated claims through `ICurrentUser`; never trust tenant or actor identifiers provided in a request body.
- Use `POST` for commands that create a resource or invoke a named business action, `PUT` for full idempotent replacement where it is genuinely appropriate, and `PATCH` only with explicit patch semantics and concurrency controls.
- Endpoints may enforce authorization policies. Handlers must still validate authorization-dependent business rules when a stale or forged request could violate an invariant.

## Commands

A command represents a single intent and contains all values needed to execute it. Its handler owns the transaction boundary and changes exactly one service's data.

```csharp
// Application/Commands/CreateInvoice/CreateInvoiceCommand.cs
public sealed record CreateInvoiceCommand(
    string TenantId,
    string ActorId,
    string IdempotencyKey,
    string ClientId,
    DateOnly IssuedOn,
    string Currency,
    IReadOnlyList<CreateInvoiceLineCommand> Lines);

public sealed record CreateInvoiceLineCommand(
    string Description,
    long UnitPriceMinor,
    int Quantity);

// Application/Commands/CreateInvoice/CreateInvoiceCommandValidator.cs
public static class CreateInvoiceCommandValidator
{
    public static ValidationResult Validate(CreateInvoiceCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(command.TenantId))
            errors["tenantId"] = ["Tenant is required."];
        if (string.IsNullOrWhiteSpace(command.ClientId))
            errors["clientId"] = ["Client is required."];
        if (command.IssuedOn > DateOnly.FromDateTime(DateTime.UtcNow))
            errors["issuedOn"] = ["Issued date cannot be in the future."];
        if (command.Currency.Length != 3)
            errors["currency"] = ["Currency must be an ISO 4217 code."];
        if (command.Lines.Count == 0)
            errors["lines"] = ["At least one invoice line is required."];

        return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Invalid(errors);
    }
}
```

Validate the request DTO at the API boundary for shape, required fields, ranges, formats, and collection limits. Validate the mapped command before the handler for command-level invariants so commands are safe to invoke from HTTP, workflows, event consumers, or tests. Avoid duplicating implementation by sharing small validation functions where it remains clear which boundary is being validated.

The command handler validates facts that require current persisted state, including tenant ownership, state transitions, concurrency, uniqueness, and domain policies.

```csharp
// Application/Commands/CreateInvoice/CreateInvoiceCommandHandler.cs
public sealed class CreateInvoiceCommandHandler(
    IInvoiceRepository invoices,
    IClientReadService clients,
    IUnitOfWork unitOfWork) : ICreateInvoiceCommandHandler
{
    public async Task<Result<CreateInvoiceResult>> HandleAsync(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var commandValidation = CreateInvoiceCommandValidator.Validate(command);
        if (!commandValidation.IsValid)
        {
            return Result.Invalid<CreateInvoiceResult>(commandValidation.Errors);
        }

        var clientExists = await clients.ExistsAsync(
            command.TenantId,
            command.ClientId,
            cancellationToken);
        if (!clientExists)
        {
            return Result.NotFound<CreateInvoiceResult>("The client does not exist.");
        }

        var existing = await unitOfWork.IdempotencyRecords.FindAsync(
            command.TenantId,
            command.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return existing.ToCreateInvoiceResult();
        }

        var invoice = Invoice.Create(
            command.TenantId,
            command.ClientId,
            command.IssuedOn,
            Currency.FromIsoCode(command.Currency),
            command.Lines.Select(line => new InvoiceLine(
                line.Description,
                Money.FromMinor(line.UnitPriceMinor, command.Currency),
                line.Quantity)));

        invoices.Add(invoice);
        unitOfWork.IdempotencyRecords.Add(IdempotencyRecord.For(
            command.TenantId,
            command.IdempotencyKey,
            invoice.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateInvoiceResult(invoice.Id, invoice.Version));
    }
}
```

- Do not invoke a command handler from a query, another handler, or an endpoint as a substitute for a defined application interface.
- A command handler does not publish domain-sync events. It only changes aggregates; `SaveChangesAsync` invokes the interceptor, which persists outbox records atomically.
- Validate references owned by another service through its System API, a local read model, or a documented integration event. Do not create cross-service foreign keys or database joins.
- Map expected failures to explicit result types: validation, not found, conflict, forbidden, or business rule violation. Reserve exceptions for unexpected failures.
- Make a retried command idempotent through a durable idempotency record unique on tenant, operation, and key. Store enough result data to return the original response.
- Use EF Core concurrency tokens for updates. Translate `DbUpdateConcurrencyException` into a `409 Conflict` response and require the client to refresh before retrying.

## Queries

Queries execute directly after validation; they do not have query handlers, mutate state, load aggregates, publish events, or use an outbox.

```csharp
// Api/Contracts/Requests/ListInvoicesRequest.cs
public sealed record ListInvoicesRequest(string? Status, int? Page, int? PageSize);

// Application/Queries/ListInvoices/ListInvoicesQuery.cs
public sealed record ListInvoicesQuery(
    string TenantId,
    string? Status,
    int Page,
    int PageSize);

// Api/Mapping/InvoiceQueryMappings.cs
public static class InvoiceQueryMappings
{
    public static ListInvoicesQuery ToQuery(this ListInvoicesRequest request, string tenantId) =>
        new(tenantId, request.Status, request.Page ?? 1, request.PageSize ?? 25);
}
```

```csharp
// Api/Endpoints/InvoiceEndpoints.cs
private static async Task<IResult> ListInvoicesAsync(
    [AsParameters] ListInvoicesRequest request,
    ICurrentUser currentUser,
    InvoicingDbContext dbContext,
    CancellationToken cancellationToken)
{
    var query = request.ToQuery(currentUser.TenantId);
    var validation = ListInvoicesQueryValidator.Validate(query);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.Errors);
    }

    var invoices = await dbContext.InvoiceReadModels
        .AsNoTracking()
        .Where(invoice => invoice.TenantId == query.TenantId)
        .Where(invoice => query.Status is null || invoice.Status == query.Status)
        .OrderByDescending(invoice => invoice.IssuedOn)
        .ThenByDescending(invoice => invoice.Id)
        .Skip((query.Page - 1) * query.PageSize)
        .Take(query.PageSize)
        .Select(invoice => new InvoiceListItemResponse(
            invoice.Id,
            invoice.Number,
            invoice.Status,
            invoice.TotalMinor,
            invoice.Currency,
            invoice.IssuedOn))
        .ToListAsync(cancellationToken);

    return Results.Ok(invoices);
}
```

- Map request DTOs to query types using extension methods, then validate the query immediately before execution.
- Use `AsNoTracking()` for read-only EF Core queries.
- Project in the database with `Select` into response DTOs. Do not call `ToListAsync` and then map entities in memory unless the data source cannot project.
- Always apply tenant and authorization scope as part of the query predicate, before pagination.
- Require explicit, bounded pagination. Apply a stable sort including a unique tie-breaker. Prefer cursor pagination for large or frequently changing datasets.
- Query only service-local read models or projections. Use a System or Process API for data owned by another service.
- Cache only explicitly stale-tolerant query results. A cache miss, eviction, or Dapr outage must not affect correctness.

## Entity Framework Core

EF Core manages persistence, not domain behavior. Configure mappings explicitly and keep database concerns in infrastructure.

```csharp
// Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).HasMaxLength(36);
        builder.Property(invoice => invoice.TenantId).HasMaxLength(36).IsRequired();
        builder.Property(invoice => invoice.Version).IsConcurrencyToken();
        builder.Property(invoice => invoice.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromIsoCode(code))
            .HasMaxLength(3)
            .IsRequired();
        builder.HasIndex(invoice => new { invoice.TenantId, invoice.Number }).IsUnique();
        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- Use migrations for every schema change. Treat migrations as reviewed, versioned source code; do not mutate a migration already applied to a shared environment.
- Configure keys, required fields, lengths, indexes, relationships, conversions, precision, and concurrency tokens explicitly.
- Store money as integer minor units plus a three-character ISO currency code. Do not persist monetary values as `float` or `double`.
- Store timestamps as UTC `DateTimeOffset`. Store accounting dates as `DateOnly`.
- Avoid generic repositories that merely wrap `DbSet`. Use a repository where it expresses an aggregate persistence boundary; use a query-specific EF Core projection for reads.
- Avoid lazy loading. Load only required relationships with explicit projection or controlled includes.
- Use a short-lived scoped `DbContext`; never cache it, share it across requests, or use it concurrently.

## Domain Changes, Interceptors, And Outbox

Aggregates collect domain events when an accepted state transition occurs. An EF Core `SaveChangesInterceptor` inspects tracked aggregates, translates domain events to versioned domain-sync event envelopes, and adds outbox messages to the same `DbContext` transaction.

```csharp
// Domain/Events/InvoiceIssued.cs
public sealed record InvoiceIssued(
    string InvoiceId,
    string TenantId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

// Infrastructure/Persistence/Interceptors/DomainEventOutboxInterceptor.cs
public sealed class DomainEventOutboxInterceptor(
    IEventEnvelopeFactory envelopeFactory) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count != 0)
            .ToArray();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(envelopeFactory.Create(domainEvent));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
```

```csharp
// Infrastructure/Persistence/InvoicingDbContext.cs
public sealed class InvoicingDbContext(
    DbContextOptions<InvoicingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

// Program.cs registration
builder.Services.AddScoped<DomainEventOutboxInterceptor>();
builder.Services.AddDbContext<InvoicingDbContext>((serviceProvider, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("invoicing"))
        .AddInterceptors(serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>()));
```

- The interceptor translates domain events to integration-safe domain-sync event envelopes. Domain events themselves never leave the service process.
- The interceptor writes only to the local outbox; it must not call Dapr, HTTP, a broker, or any remote service because EF Core save operations can retry or fail.
- Add outbox records during `SavingChanges` so aggregate state and messages commit or roll back together.
- Clear domain events only after corresponding outbox entities have been added to the change tracker. If the save fails, the unit of work must preserve enough state for its retry policy.
- An outbox dispatcher runs separately, claims messages safely, publishes through Dapr pub/sub, and records success or retry state. An inbox deduplicates every consumed event before applying local changes.
- Include message ID, event name and version, aggregate ID and version, tenant, actor, correlation ID, causation ID, occurred-at UTC time, payload, and publish state in outbox records.

## Validation, Errors, And Results

Use explicit validation and expected-failure results. FluentValidation may be used when added to the service, but validators must retain the same command/query boundaries described here.

```csharp
public static class ResultMappings
{
    public static IResult ToProblemDetails(this Failure failure) => failure.Kind switch
    {
        FailureKind.Validation => Results.ValidationProblem(failure.Errors),
        FailureKind.NotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Resource not found",
            detail: failure.Message),
        FailureKind.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: failure.Message),
        FailureKind.Forbidden => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden"),
        _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request rejected"),
    };
}
```

- Use `400` for malformed requests, `401` for unauthenticated requests, `403` for unauthorized authenticated requests, `404` when a permitted resource is absent, `409` for concurrency and state conflicts, and `422` for well-formed but semantically invalid commands when the API contract adopts it consistently.
- Keep validation error keys aligned with request DTO property names so clients can associate messages with inputs.
- Configure `AddProblemDetails` and a centralized exception handler in every API. Log unexpected exceptions with correlation context, then return a generic `500` problem response.
- Do not use exceptions for normal validation or flow control. Do not catch and discard `OperationCanceledException`.

## Security, Contracts, And Observability

- Require authentication and an explicit authorization policy on every non-public endpoint. Default-deny is preferred.
- Read tenant, actor, roles, and permissions from trusted claims or a server-side identity context. Apply tenant scope both in commands and queries. Concretely: a System API reads these from the short-lived internal JWT the Experience API issues per request — see `docs/analysis/services.md`'s Authentication & Authorization section — never from the browser's ASP.NET Core Identity cookie directly.
- Validate all identifiers, page sizes, sort fields, enum values, and date ranges. Use allowlists for sortable or filterable properties; never build dynamic SQL from request values.
- Publish OpenAPI documents for HTTP APIs and keep endpoint descriptions, response codes, DTOs, and authorization requirements current.
- Use versioned API paths or headers according to the service contract. Make additive changes compatible; release a new major version for breaking changes.
- Use structured logs and OpenTelemetry. Include service name, operation, tenant ID where permitted, actor ID where permitted, correlation ID, causation ID, aggregate ID, message ID, and outcome. Do not log tokens, credentials, raw financial data, or full request bodies.
- Apply timeouts, cancellation, and resilience policies to outbound System or Process API calls. Do not retry non-idempotent writes unless the receiving API supports idempotency.

## Unit Testing With xUnit

Use xUnit for domain behavior, command validation, command-handler outcomes, explicit DTO mappings, and query validation. Unit tests should not require PostgreSQL, Dapr, network access, clocks, or random data. Inject fakes for time, identity, repositories, and unit-of-work dependencies.

```csharp
public sealed class CreateInvoiceCommandValidatorTests
{
    [Fact]
    public void Validate_returns_error_when_the_command_has_no_lines()
    {
        var command = new CreateInvoiceCommand(
            TenantId: "tenant-1",
            ActorId: "user-1",
            IdempotencyKey: "request-1",
            ClientId: "client-1",
            IssuedOn: DateOnly.FromDateTime(DateTime.UtcNow),
            Currency: "USD",
            Lines: []);

        var result = CreateInvoiceCommandValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains("lines", result.Errors.Keys);
    }
}
```

```csharp
public sealed class InvoiceTests
{
    [Fact]
    public void Issue_transitions_a_draft_invoice_and_records_a_domain_event()
    {
        var invoice = Invoice.CreateDraft(
            tenantId: "tenant-1",
            clientId: "client-1",
            issuedOn: new DateOnly(2026, 9, 2),
            currency: Currency.FromIsoCode("USD"));

        invoice.Issue(DateTimeOffset.Parse("2026-09-02T10:00:00+00:00"));

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Contains(invoice.DomainEvents, domainEvent => domainEvent is InvoiceIssued);
    }
}
```

- Use `[Fact]` for deterministic tests and `[Theory]` with inline or member data for data variations.
- Use arrange, act, assert with one observable behavior per test. Name tests by behavior, not implementation details.
- Test command handler paths for valid execution, duplicate idempotency key, missing resource, forbidden state, business rule violation, and concurrency conflict where applicable.
- Test query validators and DTO-to-command/query extension mappings. Test database projection and interceptor behavior through dedicated EF Core integration tests when that test layer is added.
- Do not use EF Core's in-memory provider as evidence that PostgreSQL mappings, transactions, concurrency, or interceptors work. Use Testcontainers with PostgreSQL for persistence integration tests.
- Keep tests isolated and parallel-safe. Never rely on test execution order or shared mutable fixtures.

## Review Checklist

Before review, confirm:

- The endpoint accepts and returns DTOs only, maps with explicit extension methods, and has no business logic or EF Core access.
- The operation is clearly a command or a query, never both.
- Commands are validated at the DTO/command boundary and again in the handler for current-state and domain rules.
- Queries are validated immediately before direct execution and use `AsNoTracking` plus database-side DTO projection.
- Tenant scope, authorization, cancellation, pagination, and error responses are applied.
- Command changes and outbox domain-sync events are written in one EF Core transaction through the interceptor.
- Write operations use idempotency and concurrency where retries or concurrent updates are possible.
- No domain aggregate, EF entity, direct Dapr call, broker publication, or cross-service database access leaks into the API layer.
- xUnit coverage verifies the new domain rule, validation, command/query mapping, and expected handler outcome.
