using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Contapop.Bookkeeping.Service.Application.Abstractions;

namespace Contapop.Bookkeeping.Service.Infrastructure.DocumentExtraction;

public sealed class AzureDocumentExtractionAdapter(HttpClient client, IConfiguration configuration) : IDocumentExtractionAdapter
{
    public async Task<DocumentExtractionResult> ExtractAsync(ReadOnlyMemory<byte> document, CancellationToken cancellationToken)
    {
        var endpoint = configuration["DocumentExtraction:Endpoint"];
        var key = configuration["DocumentExtraction:Key"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key)) throw new DocumentExtractionException("Document extraction is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint.TrimEnd('/')}/documentintelligence/documentModels/prebuilt-invoice:analyze?api-version=2024-11-30")
        {
            Content = new ByteArrayContent(document.ToArray()),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        request.Headers.Add("Ocp-Apim-Subscription-Key", key);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Headers.Location is null) throw new DocumentExtractionException("The document extraction provider could not process the upload.");

        using var result = await PollAsync(response.Headers.Location, key, cancellationToken);
        if (!result.RootElement.TryGetProperty("analyzeResult", out var analyzeResult)
            || !analyzeResult.TryGetProperty("documents", out var documents)
            || documents.GetArrayLength() == 0
            || !documents[0].TryGetProperty("fields", out var fields))
        {
            return new DocumentExtractionResult(null, null, null, null, "No fields were extracted.");
        }
        return new(ParseAmount(fields), ParseDate(fields), ParseCategory(fields), ParseConfidence(fields), null);
    }

    private async Task<JsonDocument> PollAsync(Uri operation, string key, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, operation);
            request.Headers.Add("Ocp-Apim-Subscription-Key", key);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new DocumentExtractionException("The document extraction provider could not process the upload.");
            var result = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var status = result.RootElement.GetProperty("status").GetString();
            if (status == "succeeded") return result;
            if (status == "failed") { result.Dispose(); throw new DocumentExtractionException("The document extraction provider could not process the upload."); }
            result.Dispose();
        }
        throw new DocumentExtractionException("The document extraction provider did not complete in time.");
    }

    private static long? ParseAmount(JsonElement fields) => TryField(fields, "InvoiceTotal", out var field) && field.TryGetProperty("valueCurrency", out var currency) && currency.TryGetProperty("amount", out var amount) && amount.TryGetDecimal(out var value) ? decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.ToEven)) : null;
    private static DateOnly? ParseDate(JsonElement fields) => TryField(fields, "InvoiceDate", out var field) && field.TryGetProperty("valueDate", out var date) && DateOnly.TryParse(date.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    private static string? ParseCategory(JsonElement fields) => TryField(fields, "VendorName", out var field) && field.TryGetProperty("content", out var content) ? content.GetString()?.Trim() : null;
    private static decimal? ParseConfidence(JsonElement fields) => TryField(fields, "InvoiceTotal", out var field) && field.TryGetProperty("confidence", out var confidence) && confidence.TryGetDecimal(out var value) ? value : null;
    private static bool TryField(JsonElement fields, string name, out JsonElement field)
    {
        field = default;
        return fields.ValueKind == JsonValueKind.Object && fields.TryGetProperty(name, out field);
    }
}
