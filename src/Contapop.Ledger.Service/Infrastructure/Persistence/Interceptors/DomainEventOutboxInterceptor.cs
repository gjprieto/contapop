using System.Text.Json;
using Contapop.Ledger.Service.Domain.BankAccounts;
using Contapop.Ledger.Service.Domain.Events;
using Contapop.Ledger.Service.Domain.PaymentCards;
using Contapop.Ledger.Service.Domain.Transactions;
using Contapop.Ledger.Service.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    private static void AddOutboxMessages(DbContext? context)
    {
        if (context is null) return;

        foreach (var account in context.ChangeTracker.Entries<BankAccount>().Select(entry => entry.Entity).ToArray())
        {
            foreach (var domainEvent in account.GetDomainEvents().OfType<BankAccountLinked>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    "ledger.bank-account-linked.v1", "BankAccount", domainEvent.BankAccountId, account.Version, domainEvent.TenantId, domainEvent.OccurredAt,
                    JsonSerializer.Serialize(new { bank_account_id = domainEvent.BankAccountId, tenant_id = domainEvent.TenantId, project_id = domainEvent.ProjectId, bank_name = domainEvent.BankName, status = domainEvent.Status, created_at = domainEvent.OccurredAt })));
            }
            account.ClearDomainEvents();
        }

        foreach (var card in context.ChangeTracker.Entries<PaymentCard>().Select(entry => entry.Entity).ToArray())
        {
            foreach (var domainEvent in card.GetDomainEvents().OfType<PaymentCardLinked>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    "ledger.card-linked.v1", "PaymentCard", domainEvent.CardId, card.Version, domainEvent.TenantId, domainEvent.OccurredAt,
                    JsonSerializer.Serialize(new { card_id = domainEvent.CardId, tenant_id = domainEvent.TenantId, project_id = domainEvent.ProjectId, label = domainEvent.Label, created_at = domainEvent.OccurredAt })));
            }
            card.ClearDomainEvents();
        }

        foreach (var transaction in context.ChangeTracker.Entries<Transaction>().Select(entry => entry.Entity).ToArray())
        {
            foreach (var domainEvent in transaction.GetDomainEvents().OfType<TransactionRecorded>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    "ledger.transaction-recorded.v1", "Transaction", domainEvent.TransactionId, transaction.Version, domainEvent.TenantId, domainEvent.OccurredAt,
                    JsonSerializer.Serialize(new { transaction_id = domainEvent.TransactionId, tenant_id = domainEvent.TenantId, bank_account_id = domainEvent.BankAccountId, amount = domainEvent.AmountMinor, date = domainEvent.Date, type = domainEvent.Type, status = domainEvent.Status, created_at = domainEvent.OccurredAt })));
            }

            foreach (var domainEvent in transaction.GetDomainEvents().OfType<TransactionUpdated>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    "ledger.transaction-updated.v1", "Transaction", domainEvent.TransactionId, transaction.Version, domainEvent.TenantId, domainEvent.OccurredAt,
                    JsonSerializer.Serialize(new { transaction_id = domainEvent.TransactionId, tenant_id = domainEvent.TenantId, bank_account_id = domainEvent.BankAccountId, amount = domainEvent.AmountMinor, date = domainEvent.Date, type = domainEvent.Type, status = domainEvent.Status, updated_at = domainEvent.OccurredAt })));
            }

            foreach (var domainEvent in transaction.GetDomainEvents().OfType<TransactionArchived>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create(
                    "ledger.transaction-archived.v1", "Transaction", domainEvent.TransactionId, transaction.Version, domainEvent.TenantId, domainEvent.OccurredAt,
                    JsonSerializer.Serialize(new { transaction_id = domainEvent.TransactionId, tenant_id = domainEvent.TenantId, archived_at = domainEvent.OccurredAt })));
            }

            transaction.ClearDomainEvents();
        }
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
