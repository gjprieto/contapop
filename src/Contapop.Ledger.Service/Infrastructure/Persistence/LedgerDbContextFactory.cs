using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Contapop.Ledger.Service.Infrastructure.Persistence;

public sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTAPOP_LEDGER_CONNECTION_STRING")
            ?? "Host=localhost;Database=contapop_ledger;Username=postgres;Password=postgres";

        return new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options);
    }
}
