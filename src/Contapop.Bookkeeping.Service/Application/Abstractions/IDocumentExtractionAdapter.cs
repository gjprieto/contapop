namespace Contapop.Bookkeeping.Service.Application.Abstractions;

// Azure SDK types stay behind this application-owned boundary.
public interface IDocumentExtractionAdapter
{
    Task<DocumentExtractionResult> ExtractAsync(ReadOnlyMemory<byte> document, CancellationToken cancellationToken);
}

public sealed record DocumentExtractionResult(long? AmountMinor, DateOnly? Date, string? Category, decimal? Confidence, string? Diagnostics);

public sealed class DocumentExtractionException(string message) : Exception(message);
