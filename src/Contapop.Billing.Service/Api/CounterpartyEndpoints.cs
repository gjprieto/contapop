using System.Security.Claims;
using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Api;

public static class CounterpartyEndpoints
{
    public static IEndpointRouteBuilder MapCounterpartyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var counterparties = endpoints.MapGroup("/api/v1/counterparties").RequireAuthorization("account-owner");
        counterparties.MapPost("", CreateAsync);
        counterparties.MapPatch("/{counterpartyId:guid}", UpdateAsync);
        counterparties.MapPost("/{counterpartyId:guid}/archive", ArchiveAsync);
        counterparties.MapGet("/{counterpartyId:guid}", GetByIdAsync);
        counterparties.MapGet("", ListAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateCounterpartyRequest request, HttpContext context, CounterpartyCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        var errors = ValidateCreate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.CreateAsync(new(tenantId, key, request.Type, request.Name.Trim(), Trim(request.TaxId), Trim(request.Email), Trim(request.Address)), cancellationToken);
        return Results.Created($"/api/v1/counterparties/{result.Value!.CounterpartyId}", result.Value);
    }

    private static async Task<IResult> UpdateAsync(Guid counterpartyId, UpdateCounterpartyRequest request, HttpContext context, CounterpartyCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var errors = ValidateUpdate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.UpdateAsync(new(tenantId, key, counterpartyId, version, request.Name is null ? null : request.Name.Trim(), Trim(request.TaxId), Trim(request.Email), Trim(request.Address)), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict() : Results.Ok(result.Value);
    }

    private static async Task<IResult> ArchiveAsync(Guid counterpartyId, HttpContext context, CounterpartyCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.ArchiveAsync(new(tenantId, key, counterpartyId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict() : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetByIdAsync(Guid counterpartyId, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var counterparty = await database.Counterparties.AsNoTracking().Where(item => item.Id == counterpartyId && item.TenantId == tenantId)
            .Select(item => new CounterpartyResponse(item.Id, item.Type, item.Name, item.TaxId, item.Email, item.Address, item.Status, item.CreatedAt, item.UpdatedAt, (int)item.Version)).SingleOrDefaultAsync(cancellationToken);
        return counterparty is null ? Results.NotFound() : Results.Ok(counterparty);
    }

    private static async Task<IResult> ListAsync(string? type, string? status, string? search, string? sort, int? page, int? pageSize, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (type is not null && type is not ("customer" or "supplier") || status is not null && status is not ("active" or "archived") || sort is not null && sort is not ("name:asc" or "name:desc")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["One or more query parameters are invalid."] });
        var actualPage = Math.Max(page ?? 1, 1);
        var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = database.Counterparties.AsNoTracking().Where(item => item.TenantId == tenantId && item.Status == (status ?? "active"));
        if (type is not null) query = query.Where(item => item.Type == type);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.Name.Contains(term) || (item.TaxId != null && item.TaxId.Contains(term)) || (item.Email != null && item.Email.Contains(term))); }
        var total = await query.CountAsync(cancellationToken);
        var ordered = sort == "name:desc" ? query.OrderByDescending(item => item.Name) : query.OrderBy(item => item.Name);
        var items = await ordered.Skip((actualPage - 1) * actualPageSize).Take(actualPageSize).Select(item => new CounterpartyListItem(item.Id, item.Type, item.Name, item.TaxId, item.Email, item.Status)).ToListAsync(cancellationToken);
        return Results.Ok(new CounterpartyPagedResponse(items, actualPage, actualPageSize, total));
    }

    private static Dictionary<string, string[]> ValidateCreate(CreateCounterpartyRequest request) => Validate(request.Type, request.Name, false);
    private static Dictionary<string, string[]> ValidateUpdate(UpdateCounterpartyRequest request)
    {
        var errors = Validate(null, request.Name, true);
        if (request.Name is null && request.TaxId is null && request.Email is null && request.Address is null) errors["request"] = ["At least one field must be supplied."];
        return errors;
    }
    private static Dictionary<string, string[]> Validate(string? type, string? name, bool partial)
    {
        var errors = new Dictionary<string, string[]>();
        if (!partial && type is not ("customer" or "supplier")) errors["type"] = ["Type must be customer or supplier."];
        if ((!partial || name is not null) && (string.IsNullOrWhiteSpace(name) || name.Length > 200)) errors["name"] = ["Name of up to 200 characters is required."];
        return errors;
    }
    private static string? Trim(string? value) => value?.Trim();
    private static bool TryGetTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryGetIdempotencyKey(HttpContext context, out string key) { key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(key, out _); }
    private static bool TryGetExpectedVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; var valid = value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; if (!valid) version = 0; return valid; }
    private static IResult MissingIdempotencyKey() => Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    private static IResult Conflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The resource has changed or is archived. Refresh and try again.");
}

public sealed record CreateCounterpartyRequest(string Type, string Name, string? TaxId, string? Email, string? Address);
public sealed record UpdateCounterpartyRequest(string? Name, string? TaxId, string? Email, string? Address);
public sealed record CounterpartyListItem(Guid CounterpartyId, string Type, string Name, string? TaxId, string? Email, string Status);
public sealed record CounterpartyPagedResponse(IReadOnlyList<CounterpartyListItem> Items, int Page, int PageSize, int TotalCount);
