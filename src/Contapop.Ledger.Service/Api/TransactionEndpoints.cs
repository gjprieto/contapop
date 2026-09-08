using System.Security.Claims;
using System.Text.Json;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Contapop.Ledger.Service.Api;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var transactions = endpoints.MapGroup("/api/v1/transactions").RequireAuthorization("account-owner");
        transactions.MapPost("", RecordTransactionAsync);
        transactions.MapPost("/import", ImportTransactionsAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery();
        transactions.MapPatch("/{transactionId:guid}", UpdateTransactionAsync);
        transactions.MapPost("/{transactionId:guid}/archive", ArchiveTransactionAsync);
        transactions.MapGet("/unreconciled", ListUnreconciledTransactionsAsync);
        transactions.MapGet("/{transactionId:guid}", GetTransactionByIdAsync);
        transactions.MapGet("", ListTransactionsAsync);
        return endpoints;
    }

    private static async Task<IResult> RecordTransactionAsync(RecordTransactionRequest request, HttpContext context, TransactionCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        var errors = Validate(request.BankAccountId, request.AmountMinor, request.Date, request.Type, requireValue: true);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.RecordTransactionAsync(new(tenantId, idempotencyKey, request.BankAccountId, request.AmountMinor, request.Date, request.Type, request.Description), cancellationToken);
        return result.IsBankAccountUnavailable
            ? Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Bank account is unavailable")
            : Results.Created($"/api/v1/transactions/{result.Value!.TransactionId}", result.Value);
    }

    private static async Task<IResult> ImportTransactionsAsync([FromForm] IFormFile? file, [FromForm] Guid bankAccountId, [FromForm] string? columnMapping, HttpContext context, TransactionFileImporter importer, TransactionCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        if (file is null || file.Length == 0 || bankAccountId == Guid.Empty || string.IsNullOrWhiteSpace(columnMapping))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A non-empty file, bank account ID, and column mapping are required."] });
        }

        TransactionColumnMapping? mapping;
        try { mapping = JsonSerializer.Deserialize<TransactionColumnMapping>(columnMapping, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["columnMapping"] = ["Column mapping must be valid JSON."] }); }
        if (mapping is null || string.IsNullOrWhiteSpace(mapping.DateColumn) || string.IsNullOrWhiteSpace(mapping.AmountColumn))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["columnMapping"] = ["Date and amount columns are required."] });
        }

        TransactionFileImportParseResult parsed;
        try
        {
            await using var stream = file.OpenReadStream();
            parsed = await importer.ParseAsync(stream, file.FileName, mapping, cancellationToken);
        }
        catch (TransactionFileImportException exception)
        {
            var problem = new ProblemDetails { Status = StatusCodes.Status422UnprocessableEntity, Title = "Transaction import could not be parsed", Detail = exception.Message };
            problem.Extensions["skippedRows"] = exception.SkippedRows;
            return Results.Json(problem, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var result = await handler.ImportTransactionsAsync(new(tenantId, idempotencyKey, bankAccountId, parsed.Rows), cancellationToken);
        if (result.IsBankAccountUnavailable) return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Bank account is unavailable");
        return Results.Ok(new ImportTransactionsResponse(result.Value!.TransactionIds.Count, result.Value.TransactionIds, parsed.SkippedRows));
    }

    private static async Task<IResult> UpdateTransactionAsync(Guid transactionId, UpdateTransactionRequest request, HttpContext context, TransactionCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var errors = Validate(request.BankAccountId, request.AmountMinor, request.Date, request.Type, requireValue: false);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (request.BankAccountId is null && request.AmountMinor is null && request.Date is null && request.Type is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["At least one field must be supplied."] });
        }

        var result = await handler.UpdateTransactionAsync(new(tenantId, idempotencyKey, transactionId, version, request.BankAccountId, request.AmountMinor, request.Date, request.Type, request.Description), cancellationToken);
        return result.IsNotFound ? Results.NotFound()
            : result.IsConflict ? Conflict()
            : result.IsBankAccountUnavailable ? Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Bank account is unavailable")
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ArchiveTransactionAsync(Guid transactionId, HttpContext context, TransactionCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var idempotencyKey)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.ArchiveTransactionAsync(new(tenantId, idempotencyKey, transactionId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict() : Results.Ok(result.Value);
    }

    private static Task<IResult> ListUnreconciledTransactionsAsync(Guid? bankAccountId, string? search, string? sort, int? page, int? pageSize, HttpContext context, LedgerDbContext database, CancellationToken cancellationToken) =>
        ListTransactionsAsync(bankAccountId, null, "active", null, null, search, sort, page, pageSize, context, database, cancellationToken);

    private static async Task<IResult> ListTransactionsAsync(Guid? bankAccountId, string? type, string? status, DateOnly? dateFrom, DateOnly? dateTo, string? search, string? sort, int? page, int? pageSize, HttpContext context, LedgerDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (bankAccountId == Guid.Empty || (type is not null && type is not ("income" or "expense")) || (status is not null && status is not ("active" or "archived")) || (dateFrom is not null && dateTo is not null && dateFrom > dateTo) || !IsValidSort(sort))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["One or more query parameters are invalid."] });
        }

        var (actualPage, actualPageSize) = NormalizePaging(page, pageSize);
        var query = database.Transactions.AsNoTracking().Where(transaction => transaction.TenantId == tenantId);
        if (bankAccountId is not null) query = query.Where(transaction => transaction.BankAccountId == bankAccountId);
        if (type is not null) query = query.Where(transaction => transaction.Type == type);
        query = query.Where(transaction => transaction.Status == (status ?? "active"));
        if (dateFrom is not null) query = query.Where(transaction => transaction.Date >= dateFrom);
        if (dateTo is not null) query = query.Where(transaction => transaction.Date <= dateTo);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(transaction =>
                transaction.Type.Contains(term)
                || (transaction.Description != null && transaction.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            "date:asc" => query.OrderBy(transaction => transaction.Date).ThenBy(transaction => transaction.Id),
            "amount:asc" => query.OrderBy(transaction => transaction.AmountMinor).ThenBy(transaction => transaction.Id),
            "amount:desc" => query.OrderByDescending(transaction => transaction.AmountMinor).ThenByDescending(transaction => transaction.Id),
            _ => query.OrderByDescending(transaction => transaction.Date).ThenByDescending(transaction => transaction.Id),
        };
        var items = await ordered.Skip((actualPage - 1) * actualPageSize).Take(actualPageSize)
            .Select(transaction => new TransactionListItem(transaction.Id, transaction.BankAccountId, transaction.AmountMinor, transaction.Date, transaction.Type, transaction.Description, transaction.Status))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<TransactionListItem>(items, actualPage, actualPageSize, total));
    }

    private static async Task<IResult> GetTransactionByIdAsync(Guid transactionId, HttpContext context, LedgerDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var item = await database.Transactions.AsNoTracking()
            .Where(transaction => transaction.Id == transactionId && transaction.TenantId == tenantId)
            .Select(transaction => new TransactionDetailsResponse(transaction.Id, transaction.BankAccountId, transaction.AmountMinor, transaction.Date, transaction.Type, transaction.Description, transaction.Status, transaction.CreatedAt, transaction.UpdatedAt, (int)transaction.Version))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static Dictionary<string, string[]> Validate(Guid? bankAccountId, long? amountMinor, DateOnly? date, string? type, bool requireValue)
    {
        var errors = new Dictionary<string, string[]>();
        if ((requireValue && (bankAccountId is null || bankAccountId.Value == Guid.Empty)) || (!requireValue && bankAccountId is { } suppliedBankAccountId && suppliedBankAccountId == Guid.Empty)) errors["bankAccountId"] = ["Bank account must be a non-empty GUID."];
        if ((requireValue && amountMinor is null) || amountMinor == 0) errors["amountMinor"] = ["Amount must not be zero."];
        if (requireValue && date is null) errors["date"] = ["Date is required."];
        if ((requireValue && type is not ("income" or "expense")) || (!requireValue && type is not null && type is not ("income" or "expense"))) errors["type"] = ["Type must be income or expense."];
        return errors;
    }

    private static bool IsValidSort(string? sort) => sort is null or "date:asc" or "date:desc" or "amount:asc" or "amount:desc";
    private static bool TryGetTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryGetIdempotencyKey(HttpContext context, out string key) { key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(key, out _); }
    private static bool TryGetExpectedVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; var valid = value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; if (!valid) version = 0; return valid; }
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    private static IResult MissingIdempotencyKey() => Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    private static IResult Conflict() => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The resource has changed or is archived. Refresh and try again.");
    private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize) => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 25, 1, 100));
}

public sealed record RecordTransactionRequest(Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description = null);
public sealed record UpdateTransactionRequest(Guid? BankAccountId, long? AmountMinor, DateOnly? Date, string? Type, string? Description = null);
public sealed record ImportTransactionsResponse(int ImportedCount, IReadOnlyList<Guid> TransactionIds, IReadOnlyList<SkippedImportRow> SkippedRows);
public sealed record TransactionListItem(Guid TransactionId, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description, string Status);
public sealed record TransactionDetailsResponse(Guid TransactionId, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
