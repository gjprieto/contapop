using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Contapop.Reconciliation.Service.Infrastructure.Persistence;
public sealed class ReconciliationDbContextFactory : IDesignTimeDbContextFactory<ReconciliationDbContext>
{
    public ReconciliationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ReconciliationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("CONTAPOP_RECONCILIATION_CONNECTION_STRING") ?? "Host=localhost;Database=contapop_reconciliation;Username=postgres;Password=postgres").Options);
}
