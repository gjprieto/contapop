using System.Globalization;
using System.Text.Json;

namespace Contapop.Billing.Service.Application.Replication;

internal static class EventPayload
{
    public static Guid GetGuid(JsonElement payload, string propertyName, Guid fallback) => GetOptionalGuid(payload, propertyName) ?? fallback;
    public static Guid GetRequiredGuid(JsonElement payload, string propertyName) => GetOptionalGuid(payload, propertyName) ?? throw Missing(propertyName);
    public static Guid? GetOptionalGuid(JsonElement payload, string propertyName) => payload.TryGetProperty(propertyName, out var property) && property.TryGetGuid(out var value) ? value : null;
    public static string GetRequiredString(JsonElement payload, string propertyName) => GetOptionalString(payload, propertyName) ?? throw Missing(propertyName);
    public static string? GetOptionalString(JsonElement payload, string propertyName) => payload.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : null;
    public static long GetRequiredInt64(JsonElement payload, string propertyName) => GetOptionalInt64(payload, propertyName) ?? throw Missing(propertyName);
    public static long? GetOptionalInt64(JsonElement payload, string propertyName) => payload.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value) ? value : null;
    public static DateOnly GetRequiredDate(JsonElement payload, string propertyName) => GetOptionalDate(payload, propertyName) ?? throw Missing(propertyName);
    public static DateOnly? GetOptionalDate(JsonElement payload, string propertyName) => payload.TryGetProperty(propertyName, out var property) && DateOnly.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    public static DateTimeOffset GetTimestamp(JsonElement payload, string propertyName, DateTimeOffset fallback) => payload.TryGetProperty(propertyName, out var property) && property.TryGetDateTimeOffset(out var value) ? value : fallback;
    private static InvalidOperationException Missing(string propertyName) => new($"Integration event payload is missing '{propertyName}'.");
}
