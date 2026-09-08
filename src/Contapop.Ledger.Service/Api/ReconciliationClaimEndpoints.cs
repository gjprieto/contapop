using System.Security.Claims;
using Contapop.Ledger.Service.Application.Commands;

namespace Contapop.Ledger.Service.Api;

public static class ReconciliationClaimEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var transactions = endpoints.MapGroup("/api/v1/transactions").RequireAuthorization("account-owner");
        transactions.MapPost("/{transactionId:guid}/reconciliation-claims", ReserveAsync);
        var claims = endpoints.MapGroup("/api/v1/reconciliation-claims").RequireAuthorization("account-owner");
        claims.MapPost("/{claimId:guid}/confirm", ConfirmAsync);
        claims.MapDelete("/{claimId:guid}", ReleaseAsync);
        claims.MapPost("/{claimId:guid}/validate", ValidateAsync);
        return endpoints;
    }

    private static async Task<IResult> ReserveAsync(Guid transactionId, ReserveClaimRequest request, HttpContext context, ReconciliationClaimCommandHandler handler, CancellationToken ct)
    {
        if (!TryContext(context, out var tenantId, out var key)) return MissingContext(context);
        if (!Valid(request.DependentType, request.DependentId)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A payment, expense, or revenue dependent and a non-empty ID are required."] });
        return Map(await handler.ReserveAsync(tenantId, key, transactionId, request.DependentType, request.DependentId, ct), created: true);
    }

    private static async Task<IResult> ConfirmAsync(Guid claimId, HttpContext context, ReconciliationClaimCommandHandler handler, CancellationToken ct)
    {
        if (!TryContext(context, out var tenantId, out var key)) return MissingContext(context);
        if (!TryVersion(context, out var version)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current claim version is required."] });
        return Map(await handler.ConfirmAsync(tenantId, key, claimId, version, ct));
    }

    private static async Task<IResult> ReleaseAsync(Guid claimId, HttpContext context, ReconciliationClaimCommandHandler handler, CancellationToken ct)
    {
        if (!TryContext(context, out var tenantId, out var key)) return MissingContext(context);
        TryVersion(context, out var version);
        var result = await handler.ReleaseAsync(tenantId, key, claimId, version == 0 ? null : version, ct);
        return result.Value?.Status == "released" ? Results.NoContent() : Map(result);
    }

    private static async Task<IResult> ValidateAsync(Guid claimId, ValidateClaimRequest request, HttpContext context, ReconciliationClaimCommandHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
        if (!Valid(request.DependentType, request.DependentId)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A payment, expense, or revenue dependent and a non-empty ID are required."] });
        return await handler.ValidateAsync(tenantId, claimId, request.TransactionId, request.DependentType, request.DependentId, ct) ? Results.NoContent() : Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Claim does not match the reconciliation");
    }

    private static IResult Map(ClaimResult result, bool created = false) => result.Value is not null ? created ? Results.Created($"/api/v1/reconciliation-claims/{result.Value.ClaimId}", result.Value) : Results.Ok(result.Value) : result.Error switch { "unavailable" => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Transaction is unavailable"), "not-found" => Results.NotFound(), _ => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Reconciliation claim conflict") };
    private static bool TryContext(HttpContext context, out Guid tenantId, out string key) { tenantId = Guid.Empty; key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId) && Guid.TryParse(key, out _); }
    private static IResult MissingContext(HttpContext context) => context.User.Identity?.IsAuthenticated is true ? Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] }) : Results.Unauthorized();
    private static bool TryVersion(HttpContext context, out int version) { version = 0; var value = context.Request.Headers.IfMatch.ToString(); return value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; }
    private static bool Valid(string dependentType, Guid dependentId) => dependentId != Guid.Empty && dependentType is "payment" or "expense" or "revenue";
}
public sealed record ReserveClaimRequest(string DependentType, Guid DependentId);
public sealed record ValidateClaimRequest(Guid TransactionId, string DependentType, Guid DependentId);
