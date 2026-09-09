using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Contapop.Billing.Service.Infrastructure.Persistence;

public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=contapop_billing;Username=postgres;Password=postgres")
            .Options;

        return new BillingDbContext(options);
    }
}
