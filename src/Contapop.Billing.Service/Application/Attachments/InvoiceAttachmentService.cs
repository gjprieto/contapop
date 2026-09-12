using Contapop.Billing.Service.Infrastructure.Attachments;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.Attachments;

public sealed class InvoiceAttachmentService(BillingDbContext database, InvoiceAttachmentStorage storage)
{
    public async Task<AttachmentResult> AttachAsync(Guid tenantId, Guid invoiceId, string? requestedType, IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryValidate(file, out var contentType, out var error)) return AttachmentResult.Invalid(error!);
        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == invoiceId && item.TenantId == tenantId, cancellationToken);
        if (invoice is null) return AttachmentResult.NotFound();
        if (invoice.Status == "archived") return AttachmentResult.Conflict();

        var hasInvoiceAttachment = await database.InvoiceAttachments.AnyAsync(item => item.InvoiceId == invoiceId && item.Type == "invoice", cancellationToken);
        var type = hasInvoiceAttachment ? "other" : requestedType;
        if (type is not ("invoice" or "other")) return AttachmentResult.Invalid(hasInvoiceAttachment ? "Attachment type must be omitted after an invoice document exists." : "Choose Invoice or Other type of attachment.");
        if (hasInvoiceAttachment && requestedType is not null) return AttachmentResult.Invalid("Attachment type must be omitted after an invoice document exists.");

        var blobName = $"{tenantId:N}/{Guid.NewGuid():N}";
        await using var source = file.OpenReadStream();
        await storage.UploadAsync(blobName, source, contentType!, cancellationToken);
        var attachment = InvoiceAttachment.Create(invoiceId, tenantId, type, blobName, SafeFileName(file.FileName), contentType!, file.Length, DateTimeOffset.UtcNow);
        database.InvoiceAttachments.Add(attachment);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch
        {
            try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { }
            throw;
        }
        return AttachmentResult.Success(ToResponse(attachment), true);
    }

    public async Task<AttachmentResult> ReplaceAsync(Guid tenantId, Guid invoiceId, Guid attachmentId, int expectedVersion, IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryValidate(file, out var contentType, out var error)) return AttachmentResult.Invalid(error!);
        var attachment = await database.InvoiceAttachments.SingleOrDefaultAsync(item => item.Id == attachmentId && item.InvoiceId == invoiceId && item.TenantId == tenantId, cancellationToken);
        if (attachment is null) return AttachmentResult.NotFound();
        if (attachment.Version != expectedVersion) return AttachmentResult.Conflict();
        var blobName = $"{tenantId:N}/{Guid.NewGuid():N}";
        await using var source = file.OpenReadStream();
        await storage.UploadAsync(blobName, source, contentType!, cancellationToken);
        var oldBlobName = attachment.Replace(blobName, SafeFileName(file.FileName), contentType!, file.Length, DateTimeOffset.UtcNow);
        database.AttachmentCleanups.Add(AttachmentCleanup.Create(tenantId, oldBlobName, DateTimeOffset.UtcNow));
        try { await database.SaveChangesAsync(cancellationToken); }
        catch { try { await storage.DeleteAsync(blobName, CancellationToken.None); } catch { } throw; }
        return AttachmentResult.Success(ToResponse(attachment), false);
    }

    public async Task<bool> RemoveAsync(Guid tenantId, Guid invoiceId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await database.InvoiceAttachments.SingleOrDefaultAsync(item => item.Id == attachmentId && item.InvoiceId == invoiceId && item.TenantId == tenantId, cancellationToken);
        if (attachment is null) return false;
        database.InvoiceAttachments.Remove(attachment);
        database.AttachmentCleanups.Add(AttachmentCleanup.Create(tenantId, attachment.BlobName, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AttachmentDownloadResult?> GetAsync(Guid tenantId, Guid invoiceId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await database.InvoiceAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == attachmentId && item.InvoiceId == invoiceId && item.TenantId == tenantId, cancellationToken);
        if (attachment is null) return null;
        var download = await storage.DownloadAsync(attachment.BlobName, cancellationToken);
        return download is null ? null : new AttachmentDownloadResult(download.Content, attachment.ContentType, attachment.OriginalFileName);
    }

    public static InvoiceAttachmentResponse ToResponse(InvoiceAttachment attachment) => new(attachment.Id, attachment.InvoiceId, attachment.Type, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes, attachment.CreatedAt, attachment.UpdatedAt, (int)attachment.Version);
    private static bool TryValidate(IFormFile file, out string? contentType, out string? error)
    {
        contentType = null; error = null;
        if (file.Length == 0) { error = "The attachment file cannot be empty."; return false; }
        if (file.Length > 10_485_760) { error = "The attachment file cannot exceed 10 MB."; return false; }
        if (file.ContentType is not ("application/pdf" or "image/png" or "image/jpeg")) { error = "Only PDF, PNG, and JPEG files are supported."; return false; }
        using var stream = file.OpenReadStream(); Span<byte> header = stackalloc byte[8]; var count = stream.Read(header);
        var matches = file.ContentType switch { "application/pdf" => count >= 5 && header[..5].SequenceEqual("%PDF-"u8), "image/png" => count >= 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "image/jpeg" => count >= 3 && header[..3].SequenceEqual(new byte[] { 255, 216, 255 }), _ => false };
        if (!matches) { error = "The file contents do not match its declared type."; return false; }
        contentType = file.ContentType; return true;
    }
    private static string SafeFileName(string fileName) { var name = Path.GetFileName(fileName).Replace('\r', '_').Replace('\n', '_'); return string.IsNullOrWhiteSpace(name) ? "attachment" : name[..Math.Min(name.Length, 255)]; }
}

public sealed record InvoiceAttachmentResponse(Guid AttachmentId, Guid InvoiceId, string Type, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed record AttachmentDownloadResult(Stream Content, string ContentType, string FileName);
public sealed class AttachmentResult
{
    public InvoiceAttachmentResponse? Attachment { get; private init; }
    public bool IsNew { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public string? Error { get; private init; }
    public static AttachmentResult Success(InvoiceAttachmentResponse attachment, bool isNew) => new() { Attachment = attachment, IsNew = isNew };
    public static AttachmentResult Invalid(string error) => new() { Error = error };
    public static AttachmentResult NotFound() => new() { IsNotFound = true };
    public static AttachmentResult Conflict() => new() { IsConflict = true };
}
