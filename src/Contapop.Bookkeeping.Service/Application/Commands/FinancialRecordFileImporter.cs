using System.Globalization;
using ClosedXML.Excel;

namespace Contapop.Bookkeeping.Service.Application.Commands;

public sealed class FinancialRecordFileImporter
{
    private const int MaxRows = 10_000;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public async Task<FinancialRecordFileImportParseResult> ParseAsync(Stream file, string fileName, FinancialRecordColumnMapping mapping, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> headers;
        IReadOnlyList<IReadOnlyList<string>> rows;
        try
        {
            (headers, rows) = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".csv" => await ReadCsvAsync(file, cancellationToken),
                ".xlsx" => ReadWorkbook(file),
                _ => throw new FinancialRecordFileImportException("Only CSV and .xlsx files are supported."),
            };
        }
        catch (FinancialRecordFileImportException) { throw; }
        catch { throw new FinancialRecordFileImportException("The file could not be parsed."); }

        if (headers.Count == 0) throw new FinancialRecordFileImportException("The file must include a header row.");
        if (rows.Count > MaxRows) throw new FinancialRecordFileImportException($"The file exceeds the {MaxRows:N0}-row import limit.");
        var columns = ResolveColumns(headers, mapping);
        var validRows = new List<ImportFinancialRecordRow>();
        var skippedRows = new List<SkippedFinancialRecordRow>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                var recurring = columns.Recurring is null ? false : ParseRecurring(GetValue(row, columns.Recurring.Value));
                var interval = columns.RecurringInterval is null ? null : NullIfWhiteSpace(GetValue(row, columns.RecurringInterval.Value))?.ToLowerInvariant();
                if (!Infrastructure.Persistence.FinancialRecord.Valid(recurring, interval)) throw new FinancialRecordFileImportException("Recurring interval must be weekly, monthly, or yearly only for recurring records.");
                validRows.Add(new ImportFinancialRecordRow(ParseAmount(GetValue(row, columns.Amount)), ParseDate(GetValue(row, columns.Date)), Required(GetValue(row, columns.Category), "Category is required."), recurring, interval));
            }
            catch (FinancialRecordFileImportException exception) { skippedRows.Add(new SkippedFinancialRecordRow(index + 2, exception.Message)); }
        }

        if (validRows.Count == 0) throw new FinancialRecordFileImportException("No valid financial record rows were found.", skippedRows);
        return new FinancialRecordFileImportParseResult(validRows, skippedRows);
    }

    private static async Task<(IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>)> ReadCsvAsync(Stream file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(file, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = ParseCsv(await reader.ReadToEndAsync(cancellationToken));
        if (rows.Count == 0) throw new FinancialRecordFileImportException("The CSV file is empty.");
        return (rows[0], rows.Skip(1).ToArray());
    }

    private static (IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string>>) ReadWorkbook(Stream file)
    {
        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new FinancialRecordFileImportException("The workbook must contain a worksheet.");
        var range = sheet.RangeUsed() ?? throw new FinancialRecordFileImportException("The workbook is empty.");
        var rows = range.Rows().Select(row => (IReadOnlyList<string>)row.Cells().Select(cell => cell.GetFormattedString()).ToArray()).ToArray();
        return (rows[0], rows.Skip(1).ToArray());
    }

    private static List<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>(); var row = new List<string>(); var field = new System.Text.StringBuilder(); var quoted = false;
        var delimiter = text.Split(['\r', '\n'], 2)[0].Count(character => character == ';') > text.Split(['\r', '\n'], 2)[0].Count(character => character == ',') ? ';' : ',';
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"') { if (quoted && index + 1 < text.Length && text[index + 1] == '"') { field.Append(character); index++; } else quoted = !quoted; }
            else if (character == delimiter && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted) { if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++; row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; }
            else field.Append(character);
        }
        if (quoted) throw new FinancialRecordFileImportException("The CSV file contains an unclosed quoted value.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    private static (int Amount, int Date, int Category, int? Recurring, int? RecurringInterval) ResolveColumns(IReadOnlyList<string> headers, FinancialRecordColumnMapping mapping)
    {
        int Find(string column) => headers.Select((header, index) => (header, index)).First(pair => string.Equals(pair.header.Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase)).index;
        bool Exists(string? column) => column is not null && headers.Any(header => string.Equals(header.Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!Exists(mapping.AmountColumn) || !Exists(mapping.DateColumn) || !Exists(mapping.CategoryColumn) || (mapping.RecurringColumn is not null && !Exists(mapping.RecurringColumn)) || (mapping.RecurringIntervalColumn is not null && !Exists(mapping.RecurringIntervalColumn))) throw new FinancialRecordFileImportException("One or more mapped columns are missing from the file header.");
        return (Find(mapping.AmountColumn), Find(mapping.DateColumn), Find(mapping.CategoryColumn), mapping.RecurringColumn is null ? null : Find(mapping.RecurringColumn), mapping.RecurringIntervalColumn is null ? null : Find(mapping.RecurringIntervalColumn));
    }

    private static string GetValue(IReadOnlyList<string> row, int index) => index < row.Count ? row[index].Trim() : throw new FinancialRecordFileImportException("The row does not contain every mapped column.");
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string Required(string value, string message) => NullIfWhiteSpace(value) ?? throw new FinancialRecordFileImportException(message);
    private static DateOnly ParseDate(string value) => DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new FinancialRecordFileImportException("Date must use yyyy-MM-dd or dd/MM/yyyy.");
    private static long ParseAmount(string value)
    {
        var culture = value.Contains(',', StringComparison.Ordinal) ? SpanishCulture : CultureInfo.InvariantCulture;
        if (!decimal.TryParse(value, NumberStyles.Number, culture, out var amount) || amount <= 0) throw new FinancialRecordFileImportException("Amount must be a positive number.");
        try { return decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero)); }
        catch (OverflowException) { throw new FinancialRecordFileImportException("Amount is outside the supported range."); }
    }
    private static bool ParseRecurring(string value) => value.Trim().ToLowerInvariant() switch { "true" or "yes" or "1" => true, "false" or "no" or "0" or "" => false, _ => throw new FinancialRecordFileImportException("Recurring must be true/false, yes/no, or 1/0.") };
}

public sealed record FinancialRecordColumnMapping(string AmountColumn, string DateColumn, string CategoryColumn, string? RecurringColumn, string? RecurringIntervalColumn);
public sealed record ImportFinancialRecordRow(long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record SkippedFinancialRecordRow(int RowNumber, string Reason);
public sealed record FinancialRecordFileImportParseResult(IReadOnlyList<ImportFinancialRecordRow> Rows, IReadOnlyList<SkippedFinancialRecordRow> SkippedRows);
public sealed class FinancialRecordFileImportException(string message, IReadOnlyList<SkippedFinancialRecordRow>? skippedRows = null) : Exception(message) { public IReadOnlyList<SkippedFinancialRecordRow> SkippedRows { get; } = skippedRows ?? []; }
