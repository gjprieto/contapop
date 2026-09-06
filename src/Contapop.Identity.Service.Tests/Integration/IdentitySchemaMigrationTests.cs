using Contapop.Identity.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Identity.Service.Tests.Integration;

public sealed class IdentitySchemaMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Migrate_creates_the_identity_schemas_tables_and_columns()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var database = new IdentityDbContext(options);
        await database.Database.MigrateAsync();

        var tables = await database.Database.SqlQuery<string>($"""
            SELECT table_schema || '.' || table_name
            FROM information_schema.tables
            WHERE table_schema IN ('tenancy', 'users')
            ORDER BY table_schema, table_name
            """).ToListAsync();

        Assert.Contains("tenancy.tenants", tables);
        Assert.Contains("tenancy.projects", tables);
        Assert.Contains("tenancy.outbox_messages", tables);
        Assert.Contains("users.users", tables);

        var projectColumns = await GetColumnNamesAsync(database, "tenancy", "projects");
        var userColumns = await GetColumnNamesAsync(database, "users", "users");
        var outboxColumns = await GetColumnNamesAsync(database, "tenancy", "outbox_messages");

        Assert.Equal(
            ["created_at", "id", "name", "status", "tenant_id", "updated_at"],
            projectColumns);
        Assert.Equal(
            ["created_at", "email", "id", "language", "name", "notifications_enabled", "tenant_id", "theme", "updated_at"],
            userColumns);
        Assert.Contains("event_id", outboxColumns);
        Assert.Contains("payload", outboxColumns);
        Assert.Contains("status", outboxColumns);
    }

    private static Task<List<string>> GetColumnNamesAsync(
        IdentityDbContext database,
        string schema,
        string table) => database.Database.SqlQuery<string>($"""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = {schema} AND table_name = {table}
            ORDER BY column_name
            """).ToListAsync();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
