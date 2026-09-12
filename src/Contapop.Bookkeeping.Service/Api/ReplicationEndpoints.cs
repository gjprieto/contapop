using System.Text.Json;
using Contapop.Bookkeeping.Service.Application.Replication;
using Dapr;

namespace Contapop.Bookkeeping.Service.Api;

public static class ReplicationEndpoints
{
    public static IEndpointRouteBuilder MapReplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/events/identity", (JsonElement eventData, ProjectReplicationConsumer consumer, CancellationToken cancellationToken) =>
            ConsumeAsync(eventData, consumer.ConsumeAsync, cancellationToken))
            .WithTopic("pubsub", "identity.events")
            .WithName("ConsumeIdentityProjectEvents")
            .AllowAnonymous();

        endpoints.MapPost("/api/events/ledger", (JsonElement eventData, TransactionReplicationConsumer consumer, CancellationToken cancellationToken) =>
            ConsumeAsync(eventData, consumer.ConsumeAsync, cancellationToken))
            .WithTopic("pubsub", "ledger.events")
            .WithName("ConsumeLedgerTransactionEvents")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ConsumeAsync(JsonElement eventData, Func<IntegrationEventEnvelope, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(eventData.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (envelope is null) return Results.BadRequest();
        await consume(envelope, cancellationToken);
        return Results.Ok();
    }
}
