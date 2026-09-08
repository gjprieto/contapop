using System.Globalization;
using ClosedXML.Excel;

namespace Contapop.Ledger.Service.Application.Commands;

public sealed class TransactionFileImporter
{
    private const int MaxRows = 10_000;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public async Task<TransactionFileImportParseResult> ParseAsync(Stream file, string fileName, TransactionColumnMapping mapping, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> headers;
        IReadOnlyList<IReadOnlyList<string>> rows;
        try
        {
            (headers, rows) = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".csv" => await ReadCsvAsync(file, cancellationToken),
                ".xlsx" => ReadWorkbook(file),
                _ => throw new TransactionFileImportException("Only CSV and .xlsx files are supported."),
            };
        }
        catch (TransactionFileImportException)
        {
            throw;
        }
        catch
        {
            throw new TransactionFileImportException("The file could not be parsed.");
        }

        if (headers.Count == 0) throw new TransactionFileImportException("The file must include a header row.");
        if (rows.Count > MaxRows) throw new TransactionFileImportException($"The file exceeds the {MaxRows:N0}-row import limit.");

        var columnIndexes = ResolveColumns(headers, mapping);
        var validRows = new List<ImportTransactionRow>();
        var skippedRows = new List<SkippedImportRow>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            try
            {
                var amountMinor = ParseAmount(GetValue(row, columnIndexes.Amount));
                var type = mapping.TypeColumn is null
                    ? amountMinor < 0 ? "expense" : "income"
                    : ParseType(GetValue(row, columnIndexes.Type!.Value));
                validRows.Add(new ImportTransactionRow(
                    ParseDate(GetValue(row, columnIndexes.Date)),
                    amountMinor,
                    type,
                    ParseDescription(columnIndexes.Description is { } descriptionIndex ? GetValue(row, descriptionIndex) : null)));
            }
            catch (TransactionFileImportException exception)
            {
                skippedRows.Add(new SkippedImportRow(index + 2, exception.Message));
            }
        }

        if (validRows.Count == 0) throw new TransactionFileImportException("No valid transaction rows were found.", skippedRows);
        return new TransactionFileImportParseResult(validRows, skippedRows);
    }

    private static async Task<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> ReadCsvAsync(Stream file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(file, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var rows = ParseCsv(text);
        if (rows.Count == 0) throw new TransactionFileImportException("The CSV file is empty.");
        return (rows[0], rows.Skip(1).ToArray());
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) ReadWorkbook(Stream file)
    {
        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new TransactionFileImportException("The workbook must contain a worksheet.");
        var range = sheet.RangeUsed() ?? throw new TransactionFileImportException("The workbook is empty.");
        var rows = range.Rows().Select(row => (IReadOnlyList<string>)row.Cells().Select(cell => cell.GetFormattedString()).ToArray()).ToArray();
        return (rows[0], rows.Skip(1).ToArray());
    }

    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var delimiter = DetectDelimiter(text);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"') { field.Append(character); index++; }
                else inQuotes = !inQuotes;
            }
            else if (character == delimiter && !inQuotes) { row.Add(field.ToString()); field.Clear(); }
            else if ((character == '\r' || character == '\n') && !inQuotes)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = [];
            }
            else field.Append(character);
        }

        if (inQuotes) throw new TransactionFileImportException("The CSV file contains an unclosed quoted value.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split(['\r', '\n'], 2)[0];
        return firstLine.Count(character => character == ';') > firstLine.Count(character => character == ',') ? ';' : ',';
    }

    private static (int Date, int Amount, int? Type, int? Description) ResolveColumns(IReadOnlyList<string> headers, TransactionColumnMapping mapping)
    {
        int Find(string column) => headers.Select((header, index) => (header, index)).FirstOrDefault(pair => string.Equals(pair.header.Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase)).index;
        bool Exists(string column) => headers.Any(header => string.Equals(header.Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!Exists(mapping.DateColumn) || !Exists(mapping.AmountColumn) || (mapping.TypeColumn is not null && !Exists(mapping.TypeColumn)) || (mapping.DescriptionColumn is not null && !Exists(mapping.DescriptionColumn)))
            throw new TransactionFileImportException("One or more mapped columns are missing from the file header.");
        return (Find(mapping.DateColumn), Find(mapping.AmountColumn), mapping.TypeColumn is null ? null : Find(mapping.TypeColumn), mapping.DescriptionColumn is null ? null : Find(mapping.DescriptionColumn));
    }

    private static string GetValue(IReadOnlyList<string> row, int index) => index < row.Count ? row[index].Trim() : throw new TransactionFileImportException("The row does not contain every mapped column.");
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string? ParseDescription(string? value)
    {
        if (value is null) return null;
        var description = NullIfWhiteSpace(value);
        if (description?.Length > 1_000) throw new TransactionFileImportException("Description must not exceed 1,000 characters.");
        return description;
    }
    private static DateOnly ParseDate(string value)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : throw new TransactionFileImportException("Date must use yyyy-MM-dd or dd/MM/yyyy.");
    }

    private static long ParseAmount(string value)
    {
        var culture = value.Contains(',', StringComparison.Ordinal) ? SpanishCulture : CultureInfo.InvariantCulture;
        if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, culture, out var amount))
            throw new TransactionFileImportException("Amount is not a valid number.");
        if (amount == 0) throw new TransactionFileImportException("Amount must not be zero.");
        try { return decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero)); }
        catch (OverflowException) { throw new TransactionFileImportException("Amount is outside the supported range."); }
    }

    private static string ParseType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "income" or "ingreso" or "ingresos" or "credit" or "abono" => "income",
        "expense" or "gasto" or "gastos" or "debit" or "cargo" => "expense",
        _ => throw new TransactionFileImportException("Type must be income or expense."),
    };
}

public sealed record TransactionColumnMapping(string DateColumn, string AmountColumn, string? TypeColumn, string? DescriptionColumn);
public sealed record SkippedImportRow(int RowNumber, string Reason);
public sealed record TransactionFileImportParseResult(IReadOnlyList<ImportTransactionRow> Rows, IReadOnlyList<SkippedImportRow> SkippedRows);

public sealed class TransactionFileImportException(string message, IReadOnlyList<SkippedImportRow>? skippedRows = null) : Exception(message)
{
    public IReadOnlyList<SkippedImportRow> SkippedRows { get; } = skippedRows ?? [];
}
