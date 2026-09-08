using System.Security.Claims;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Api;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/api/v1").RequireAuthorization("account-owner");
        accounts.MapPost("/bank-accounts", LinkBankAccountAsync);
        accounts.MapPost("/bank-accounts/{bankAccountId:guid}/archive", ArchiveBankAccountAsync);
        accounts.MapGet("/bank-accounts", ListBankAccountsAsync);
        accounts.MapPost("/payment-cards", AddPaymentCardLabelAsync);
        accounts.MapDelete("/payment-cards/{cardId:guid}", RemovePaymentCardLabelAsync);
        accounts.MapGet("/payment-cards", ListPaymentCardsAsync);
        return endpoints;
    }

    private static async Task<IResult> LinkBankAccountAsync(LinkBankAccountRequest request, HttpContext context, AccountCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        var errors = Validate(request.ProjectId, request.AccountNumber, request.BankName);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.LinkBankAccountAsync(new(tenantId, idempotencyKey, request.ProjectId, request.AccountNumber, request.BankName), cancellationToken);
        return result.IsProjectUnavailable
            ? Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Project is unavailable")
            : Results.Created($"/api/v1/bank-accounts/{result.Value!.BankAccountId}", result.Value);
    }

    private static async Task<IResult> ArchiveBankAccountAsync(Guid bankAccountId, HttpContext context, AccountCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.ArchiveBankAccountAsync(new(tenantId, idempotencyKey, bankAccountId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict() : Results.Ok(result.Value);
    }

    private static async Task<IResult> AddPaymentCardLabelAsync(AddPaymentCardLabelRequest request, HttpContext context, AccountCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        var errors = Validate(request.ProjectId, request.Label, request.CardholderName);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.AddPaymentCardLabelAsync(new(tenantId, idempotencyKey, request.ProjectId, request.Label, request.CardholderName, request.ExpirationDate), cancellationToken);
        return result.IsProjectUnavailable
            ? Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Project is unavailable")
            : Results.Created($"/api/v1/payment-cards/{result.Value!.CardId}", result.Value);
    }

    private static async Task<IResult> RemovePaymentCardLabelAsync(Guid cardId, HttpContext context, AccountCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.RemovePaymentCardLabelAsync(new(tenantId, idempotencyKey, cardId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict() : Results.NoContent();
    }

    private static async Task<IResult> ListBankAccountsAsync(string? status, int? page, int? pageSize, HttpContext context, LedgerDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (status is not null && status is not ("active" or "archived")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status must be active or archived."] });
        var (actualPage, actualPageSize) = NormalizePaging(page, pageSize);
        var query = database.BankAccounts.AsNoTracking().Where(account => account.TenantId == tenantId && (status ?? "active") == account.Status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(account => account.BankName).Skip((actualPage - 1) * actualPageSize).Take(actualPageSize)
            .Select(account => new BankAccountListItem(account.Id, account.AccountNumber, account.BankName, account.Status,
                database.Transactions.Where(transaction => transaction.BankAccountId == account.Id && transaction.Status == "active").Sum(transaction => (long?)transaction.AmountMinor) ?? 0, account.Version))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<BankAccountListItem>(items, actualPage, actualPageSize, total));
    }

    private static async Task<IResult> ListPaymentCardsAsync(int? page, int? pageSize, HttpContext context, LedgerDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var (actualPage, actualPageSize) = NormalizePaging(page, pageSize);
        var query = database.PaymentCards.AsNoTracking().Where(card => card.TenantId == tenantId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(card => card.Label).Skip((actualPage - 1) * actualPageSize).Take(actualPageSize)
            .Select(card => new PaymentCardListItem(card.Id, card.Label, card.CardholderName, card.ExpirationDate, card.Version)).ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<PaymentCardListItem>(items, actualPage, actualPageSize, total));
    }

    private static Dictionary<string, string[]> Validate(Guid projectId, string? first, string? second)
    {
        var errors = new Dictionary<string, string[]>();
        if (projectId == Guid.Empty) errors["projectId"] = ["Project is required."];
        if (string.IsNullOrWhiteSpace(first) || first.Length > 200) errors["value"] = ["A value of up to 200 characters is required."];
        if (string.IsNullOrWhiteSpace(second) || second.Length > 200) errors["secondaryValue"] = ["A value of up to 200 characters is required."];
        return errors;
    }

    private static bool TryGetTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryGetIdempotencyKey(HttpContext context, out string key)
    {
        key = context.Request.Headers["Idempotency-Key"].ToString();
        return Guid.TryParse(key, out _);
    }
    private static bool TryGetExpectedVersion(HttpContext context, out int version)
    {
        var value = context.Request.Headers.IfMatch.ToString();
        version = 0;
        var valid = value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0;
        if (!valid) version = 0;
        return valid;
    }
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    private static IResult MissingIdempotencyKey() => Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    private static IResult Conflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The resource has changed. Refresh and try again.");
    private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize) => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 25, 1, 100));
}

public sealed record LinkBankAccountRequest(Guid ProjectId, string AccountNumber, string BankName);
public sealed record AddPaymentCardLabelRequest(Guid ProjectId, string Label, string CardholderName, DateOnly? ExpirationDate);
public sealed record BankAccountListItem(Guid BankAccountId, string AccountNumber, string BankName, string Status, long BalanceMinor, int Version);
public sealed record PaymentCardListItem(Guid CardId, string Label, string CardholderName, DateOnly? ExpirationDate, int Version);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
