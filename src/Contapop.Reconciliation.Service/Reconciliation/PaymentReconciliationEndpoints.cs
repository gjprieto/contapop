using System.Security.Claims;
using Contapop.Reconciliation.Service.Infrastructure.Persistence;

namespace Contapop.Reconciliation.Service.Reconciliation;

public static class PaymentReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapPaymentReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/payment-reconciliations", StartAsync).RequireAuthorization("account-owner");
        return endpoints;
    }

    private static async Task<IResult> StartAsync(StartPaymentReconciliationRequest request, HttpContext context, PaymentReconciliationCoordinator coordinator, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
        if (request.TransactionId == Guid.Empty || request.PaymentId == Guid.Empty || request.PaymentVersion <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Transaction ID, payment ID, and a positive payment version are required."] });

        var operation = await coordinator.StartAsync(tenantId, request, key, cancellationToken);
        await coordinator.ExecuteAsync(operation.Id, cancellationToken);
        var completed = await coordinator.GetAsync(operation.Id, cancellationToken);
        return Results.Accepted($"/internal/v1/payment-reconciliations/{operation.Id}", new PaymentReconciliationResponse(operation.Id, completed?.Status ?? operation.Status));
    }
}

public sealed record PaymentReconciliationResponse(Guid OperationId, string Status);
