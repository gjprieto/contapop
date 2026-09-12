namespace Contapop.Billing.Service.Infrastructure.Persistence;

public sealed class Invoice
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid CounterpartyId { get; private set; }
    public string Direction { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public long NetAmountMinor { get; private set; }
    public long TaxAmountMinor { get; private set; }
    public long TotalAmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public DateOnly DueDate { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<InvoiceLine> Lines { get; private set; } = [];

    public static Invoice Create(Guid tenantId, Guid projectId, Guid counterpartyId, string direction, string type, IReadOnlyList<CreateInvoiceLine> lines, DateOnly date, DateOnly dueDate, DateTimeOffset now)
    {
        var invoiceLines = lines.Select(line => InvoiceLine.Create(line.Description, line.Quantity, line.UnitPriceMinor, line.TaxRate)).ToList();
        var netAmountMinor = invoiceLines.Sum(line => line.NetAmountMinor);
        var taxAmountMinor = invoiceLines.Sum(line => line.TaxAmountMinor);
        return new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectId, CounterpartyId = counterpartyId,
            Direction = direction, Type = type, Status = "draft", NetAmountMinor = netAmountMinor,
            TaxAmountMinor = taxAmountMinor, TotalAmountMinor = checked(netAmountMinor + taxAmountMinor),
            Date = date, DueDate = dueDate, Version = 1, CreatedAt = now, UpdatedAt = now, Lines = invoiceLines,
        };
    }

    // Retained for existing internal test fixtures that exercise invoice lifecycle independent of lines.
    public static Invoice Create(Guid tenantId, Guid projectId, Guid counterpartyId, string direction, long netAmountMinor, decimal taxRate, DateOnly date, DateOnly dueDate, DateTimeOffset now) =>
        Create(tenantId, projectId, counterpartyId, direction, "service", [new CreateInvoiceLine("Invoice amount", 1, netAmountMinor, taxRate)], date, dueDate, now);

    public bool TryIssue(int expectedVersion, DateTimeOffset now)
    {
        if (Status != "draft" || Version != expectedVersion) return false;
        Status = "issued";
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryUpdateDraft(int expectedVersion, IReadOnlyList<CreateInvoiceLine> lines, DateOnly date, DateOnly dueDate, DateTimeOffset now)
    {
        if (Status != "draft" || Version != expectedVersion) return false;

        var invoiceLines = lines.Select(line => InvoiceLine.Create(line.Description, line.Quantity, line.UnitPriceMinor, line.TaxRate)).ToList();
        Lines = invoiceLines;
        NetAmountMinor = invoiceLines.Sum(line => line.NetAmountMinor);
        TaxAmountMinor = invoiceLines.Sum(line => line.TaxAmountMinor);
        TotalAmountMinor = checked(NetAmountMinor + TaxAmountMinor);
        Date = date;
        DueDate = dueDate;
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryVoid(int expectedVersion, DateTimeOffset now)
    {
        if (Status != "draft" || Version != expectedVersion) return false;
        Status = "void";
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryArchive(int expectedVersion, DateTimeOffset now)
    {
        if (Status is not ("issued" or "overdue" or "void") || Version != expectedVersion) return false;
        Status = "archived";
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryMarkPaid(DateTimeOffset now)
    {
        if (Status is not ("issued" or "overdue")) return false;
        Status = "paid";
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryMarkOverdue(DateOnly currentDate, DateTimeOffset now)
    {
        if (Status != "issued" || DueDate >= currentDate) return false;
        Status = "overdue";
        Version++;
        UpdatedAt = now;
        return true;
    }
}

public sealed class InvoiceLine
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public decimal TaxRate { get; private set; }
    public long NetAmountMinor { get; private set; }
    public long TaxAmountMinor { get; private set; }
    public long TotalAmountMinor { get; private set; }

    public static InvoiceLine Create(string description, int quantity, long unitPriceMinor, decimal taxRate)
    {
        var netAmountMinor = checked(quantity * unitPriceMinor);
        var taxAmountMinor = decimal.ToInt64(decimal.Round(netAmountMinor * taxRate, 0, MidpointRounding.ToEven));
        return new()
        {
            Id = Guid.NewGuid(), Description = description, Quantity = quantity, UnitPriceMinor = unitPriceMinor,
            TaxRate = taxRate, NetAmountMinor = netAmountMinor, TaxAmountMinor = taxAmountMinor,
            TotalAmountMinor = checked(netAmountMinor + taxAmountMinor),
        };
    }
}

public sealed class InvoiceAttachment
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid TenantId { get; private set; }
    public string BlobName { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static InvoiceAttachment Create(Guid invoiceId, Guid tenantId, string blobName, string originalFileName, string contentType, long sizeBytes, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), InvoiceId = invoiceId, TenantId = tenantId, BlobName = blobName,
        OriginalFileName = originalFileName, ContentType = contentType, SizeBytes = sizeBytes,
        CreatedAt = now, UpdatedAt = now,
    };

    public string Replace(string blobName, string originalFileName, string contentType, long sizeBytes, DateTimeOffset now)
    {
        var previousBlobName = BlobName;
        BlobName = blobName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UpdatedAt = now;
        return previousBlobName;
    }
}

public sealed class AttachmentCleanup
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string BlobName { get; private set; } = null!;
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AttachmentCleanup Create(Guid tenantId, string blobName, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, BlobName = blobName, NextAttemptAt = now, CreatedAt = now,
    };

    public void Retry(DateTimeOffset now)
    {
        AttemptCount++;
        NextAttemptAt = now.AddMinutes(Math.Min(60, Math.Max(1, AttemptCount)));
    }
}

public sealed record CreateInvoiceLine(string Description, int Quantity, long UnitPriceMinor, decimal TaxRate);

public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid? ReconciledTransactionId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public string PaymentMethod { get; private set; } = null!;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment Create(Guid tenantId, Guid invoiceId, long amountMinor, DateOnly date, string paymentMethod, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, InvoiceId = invoiceId, AmountMinor = amountMinor, Date = date,
        PaymentMethod = paymentMethod, Version = 1, CreatedAt = now, UpdatedAt = now,
    };

    public bool TryReconcile(Guid transactionId, int expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion || (ReconciledTransactionId is not null && ReconciledTransactionId != transactionId)) return false;
        if (ReconciledTransactionId == transactionId) return true;
        ReconciledTransactionId = transactionId;
        Version++;
        UpdatedAt = now;
        return true;
    }
}

public sealed class Counterparty
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? TaxId { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string Status { get; private set; } = null!;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Counterparty Create(Guid tenantId, string type, string name, string? taxId, string? email, string? address, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Type = type,
        Name = name,
        TaxId = taxId,
        Email = email,
        Address = address,
        Status = "active",
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public bool TryUpdate(int expectedVersion, string? name, string? taxId, string? email, string? address, DateTimeOffset now)
    {
        if (Status != "active" || Version != expectedVersion) return false;
        if (name is not null) Name = name;
        if (taxId is not null) TaxId = taxId;
        if (email is not null) Email = email;
        if (address is not null) Address = address;
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool TryArchive(int expectedVersion, DateTimeOffset now)
    {
        if (Status != "active" || Version != expectedVersion) return false;
        Status = "archived";
        Version++;
        UpdatedAt = now;
        return true;
    }
}

public sealed class InboxMessage
{
    public string ConsumerName { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Processed(Guid eventId, string consumerName, DateTimeOffset processedAt) => new()
    {
        EventId = eventId,
        ConsumerName = consumerName,
        ProcessedAt = processedAt,
    };
}

public sealed class IdempotencyRecord
{
    public Guid TenantId { get; private set; }
    public string Operation { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(Guid tenantId, string operation, string key, string result, DateTimeOffset createdAt) => new()
    {
        TenantId = tenantId,
        Operation = operation,
        Key = key,
        Result = result,
        CreatedAt = createdAt,
    };
}

public sealed class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Payload { get; private set; } = null!;
    public string Status { get; private set; } = null!;

    public static OutboxMessage Create(string eventName, string aggregateType, Guid aggregateId, long aggregateVersion, Guid tenantId, DateTimeOffset occurredAt, string payload, Guid? correlationId = null, Guid? causationId = null) => new()
    {
        EventId = Guid.NewGuid(), EventName = eventName, AggregateType = aggregateType, AggregateId = aggregateId,
        AggregateVersion = aggregateVersion, TenantId = tenantId, CorrelationId = correlationId, CausationId = causationId,
        OccurredAt = occurredAt, Payload = payload, Status = "pending",
    };
}
