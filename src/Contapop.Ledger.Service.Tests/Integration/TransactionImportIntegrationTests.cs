using System.Text.Json;
using ClosedXML.Excel;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class TransactionImportIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Csv_import_creates_transactions_and_one_recorded_event_per_valid_row()
    {
        var tenantId = Guid.NewGuid();
        var bankAccountId = await SeedActiveBankAccountAsync(tenantId);
        var importer = new TransactionFileImporter();
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Fecha;Importe;Tipo;Concepto\n08/09/2026;-12,50;gasto;Cafe\n2026-09-09;100.00;income;Invoice\n"));
        var parsed = await importer.ParseAsync(stream, "statement.csv", new("Fecha", "Importe", "Tipo", "Concepto"), CancellationToken.None);
        await using var database = new LedgerDbContext(CreateOptions());

        var result = await new TransactionCommandHandler(database).ImportTransactionsAsync(
            new(tenantId, Guid.NewGuid().ToString(), bankAccountId, parsed.Rows), CancellationToken.None);

        Assert.Equal(2, result.Value!.TransactionIds.Count);
        Assert.Empty(parsed.SkippedRows);
        var transactions = await database.Transactions.OrderBy(transaction => transaction.Date).ToListAsync();
        Assert.Collection(transactions,
            transaction => { Assert.Equal(-1_250, transaction.AmountMinor); Assert.Equal("expense", transaction.Type); Assert.Equal("Cafe", transaction.Description); },
            transaction => { Assert.Equal(10_000, transaction.AmountMinor); Assert.Equal("income", transaction.Type); Assert.Equal("Invoice", transaction.Description); });
        Assert.Equal(2, await database.OutboxMessages.CountAsync(message => message.EventName == "ledger.transaction-recorded.v1"));
    }

    [Fact]
    public async Task Malformed_file_fails_before_any_transaction_can_be_imported()
    {
        var importer = new TransactionFileImporter();
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Fecha;Importe\n08/09/2026;12,50\""));

        var exception = await Assert.ThrowsAsync<TransactionFileImportException>(() => importer.ParseAsync(stream, "statement.csv", new("Fecha", "Importe", null, null), CancellationToken.None));

        Assert.Equal("The CSV file contains an unclosed quoted value.", exception.Message);
    }

    [Fact]
    public async Task Xlsx_import_accepts_spanish_dates_and_amounts()
    {
        var importer = new TransactionFileImporter();
        await using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Transactions");
            sheet.Cell("A1").Value = "Fecha";
            sheet.Cell("B1").Value = "Importe";
            sheet.Cell("C1").Value = "Concepto";
            sheet.Cell("A2").Value = "08/09/2026";
            sheet.Cell("B2").Value = "1.234,56";
            sheet.Cell("C2").Value = "Factura";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;

        var parsed = await importer.ParseAsync(stream, "statement.xlsx", new("Fecha", "Importe", null, "Concepto"), CancellationToken.None);

        var row = Assert.Single(parsed.Rows);
        Assert.Equal(new DateOnly(2026, 9, 8), row.Date);
        Assert.Equal(123_456, row.AmountMinor);
        Assert.Equal("income", row.Type);
        Assert.Equal("Factura", row.Description);
    }

    private async Task<Guid> SeedActiveBankAccountAsync(Guid tenantId)
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        var account = Contapop.Ledger.Service.Domain.BankAccounts.BankAccount.Create(tenantId, Guid.NewGuid(), "ES9121000418450200051332", "CaixaBank", DateTimeOffset.UtcNow);
        database.BankAccounts.Add(account);
        await database.SaveChangesAsync();
        database.OutboxMessages.RemoveRange(database.OutboxMessages);
        await database.SaveChangesAsync();
        return account.Id;
    }

    private DbContextOptions<LedgerDbContext> CreateOptions() => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new DomainEventOutboxInterceptor())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
