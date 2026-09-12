using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence;

public sealed class BookkeepingDbContextFactory : IDesignTimeDbContextFactory<BookkeepingDbContext>
{
    public BookkeepingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTAPOP_BOOKKEEPING_CONNECTION_STRING")
            ?? "Host=localhost;Database=contapop_bookkeeping;Username=postgres;Password=postgres";

        return new BookkeepingDbContext(new DbContextOptionsBuilder<BookkeepingDbContext>()
            .UseNpgsql(connectionString)
            .Options);
    }
}
