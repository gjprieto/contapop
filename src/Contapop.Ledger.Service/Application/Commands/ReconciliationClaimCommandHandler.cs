using System.Text.Json;
using Contapop.Ledger.Service.Domain.Reconciliation;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Application.Commands;

public sealed class ReconciliationClaimCommandHandler(LedgerDbContext database)
{
    public async Task<ClaimResult> ReserveAsync(Guid tenantId, string key, Guid transactionId, string dependentType, Guid dependentId, CancellationToken ct)
    {
        var replay = await ReplayAsync(tenantId, "reserve-reconciliation", key, ct);
        if (replay is not null) return ClaimResult.Success(replay);
        if (!await database.Transactions.AnyAsync(t => t.Id == transactionId && t.TenantId == tenantId && t.Status == "active", ct)) return ClaimResult.Unavailable();
        var existing = await database.ReconciliationClaims.SingleOrDefaultAsync(c => c.TenantId == tenantId && (c.TransactionId == transactionId || (c.DependentType == dependentType && c.DependentId == dependentId)) && c.Status != "released", ct);
        if (existing is not null) return existing.Matches(transactionId, dependentType, dependentId) ? ClaimResult.Success(ToResult(existing)) : ClaimResult.Conflict();
        var claim = TransactionReconciliationClaim.Reserve(tenantId, transactionId, dependentType, dependentId, DateTimeOffset.UtcNow);
        var result = ToResult(claim);
        database.ReconciliationClaims.Add(claim);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(tenantId, "reserve-reconciliation", key, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        try { await database.SaveChangesAsync(ct); return ClaimResult.Success(result); }
        catch (DbUpdateException) { return ClaimResult.Conflict(); }
    }

    public async Task<ClaimResult> ConfirmAsync(Guid tenantId, string key, Guid claimId, int version, CancellationToken ct) =>
        await ChangeAsync(tenantId, key, claimId, version, true, ct);

    public async Task<ClaimResult> ReleaseAsync(Guid tenantId, string key, Guid claimId, int? version, CancellationToken ct) =>
        await ChangeAsync(tenantId, key, claimId, version, false, ct);

    public async Task<bool> ValidateAsync(Guid tenantId, Guid claimId, Guid transactionId, string type, Guid dependentId, CancellationToken ct)
    {
        var claim = await database.ReconciliationClaims.AsNoTracking().SingleOrDefaultAsync(c => c.Id == claimId && c.TenantId == tenantId, ct);
        return claim is not null && claim.Status == "reserved" && claim.ExpiresAt > DateTimeOffset.UtcNow && claim.Matches(transactionId, type, dependentId);
    }

    private async Task<ClaimResult> ChangeAsync(Guid tenantId, string key, Guid claimId, int? version, bool confirm, CancellationToken ct)
    {
        var operation = confirm ? "confirm-reconciliation" : "release-reconciliation";
        var replay = await ReplayAsync(tenantId, operation, key, ct); if (replay is not null) return ClaimResult.Success(replay);
        var claim = await database.ReconciliationClaims.SingleOrDefaultAsync(c => c.Id == claimId && c.TenantId == tenantId, ct); if (claim is null) return ClaimResult.NotFound();
        var changed = confirm ? version is not null && claim.TryConfirm(version.Value, DateTimeOffset.UtcNow) : claim.TryRelease(version, DateTimeOffset.UtcNow);
        if (!changed) return ClaimResult.Conflict();
        var result = ToResult(claim); database.IdempotencyRecords.Add(IdempotencyRecord.Create(tenantId, operation, key, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(ct); return ClaimResult.Success(result);
    }
    private async Task<ReconciliationClaimResult?> ReplayAsync(Guid tenantId, string operation, string key, CancellationToken ct) => JsonSerializer.Deserialize<ReconciliationClaimResult?>(await database.IdempotencyRecords.AsNoTracking().Where(r => r.TenantId == tenantId && r.Operation == operation && r.Key == key).Select(r => r.Result).SingleOrDefaultAsync(ct) ?? "null");
    private static ReconciliationClaimResult ToResult(TransactionReconciliationClaim c) => new(c.Id, c.TransactionId, c.DependentType, c.DependentId, c.Status, c.ExpiresAt, c.ConfirmedAt, (int)c.Version);
}
public sealed record ReconciliationClaimResult(Guid ClaimId, Guid TransactionId, string DependentType, Guid DependentId, string Status, DateTimeOffset ExpiresAt, DateTimeOffset? ConfirmedAt, int Version);
public sealed class ClaimResult { public ReconciliationClaimResult? Value { get; private init; } public string? Error { get; private init; } public static ClaimResult Success(ReconciliationClaimResult value) => new() { Value = value }; public static ClaimResult Conflict() => new() { Error = "conflict" }; public static ClaimResult Unavailable() => new() { Error = "unavailable" }; public static ClaimResult NotFound() => new() { Error = "not-found" }; }
