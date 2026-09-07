using System.Text.Json;
using Contapop.Ledger.Service.Application.ProjectReplication;

namespace Contapop.Ledger.Service.Api;

public static class ProjectReplicationEndpoints
{
    public static IEndpointRouteBuilder MapProjectReplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/events/identity", async (
            JsonElement eventData,
            ProjectReplicationConsumer consumer,
            CancellationToken cancellationToken) =>
        {
            var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(eventData.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (envelope is null)
            {
                return Results.BadRequest();
            }

            await consumer.ConsumeAsync(envelope, cancellationToken);
            return Results.Ok();
        })
        .WithName("ConsumeIdentityProjectEvents")
        .AllowAnonymous();

        return endpoints;
    }
}
