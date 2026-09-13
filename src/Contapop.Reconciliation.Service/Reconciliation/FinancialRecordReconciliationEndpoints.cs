using System.Security.Claims;

namespace Contapop.Reconciliation.Service.Reconciliation;

public static class FinancialRecordReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapFinancialRecordReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/financial-record-reconciliations", StartAsync).RequireAuthorization("account-owner");
        return endpoints;
    }

    private static async Task<IResult> StartAsync(StartFinancialRecordReconciliationRequest request, HttpContext context, FinancialRecordReconciliationCoordinator coordinator, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
        if (request.TransactionId == Guid.Empty || request.RecordId == Guid.Empty || request.RecordVersion <= 0 || request.RecordType is not ("expense" or "revenue")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Transaction ID, record ID, positive record version, and expense or revenue type are required."] });
        var operation = await coordinator.StartAsync(tenantId, request, key, ct);
        await coordinator.ExecuteAsync(operation.Id, ct);
        return Results.Accepted($"/internal/v1/financial-record-reconciliations/{operation.Id}", new FinancialRecordReconciliationResponse(operation.Id));
    }
}

public sealed record FinancialRecordReconciliationResponse(Guid OperationId);
