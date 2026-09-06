using Contapop.Identity.Service.Infrastructure.Persistence;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Time;
using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Application.Commands.ProvisionTenant;
using Contapop.Identity.Service.Api.Contracts;
using Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddScoped<DomainEventOutboxInterceptor>();
builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity"))
        .AddInterceptors(serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>()));
builder.Services.AddIdentityCore<IdentityCredential>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>();
builder.Services.AddScoped<IPasswordHasher<IdentityCredential>, PasswordHasher<IdentityCredential>>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ProvisionTenantCommandHandler>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await database.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok());

app.MapPost("/api/v1/tenants", async (
    ProvisionTenantRequest request,
    ProvisionTenantCommandHandler handler,
    CancellationToken cancellationToken) =>
{
    var command = new ProvisionTenantCommand(request.TenantName, request.OwnerName, request.OwnerEmail, request.InitialPassword);
    var errors = ProvisionTenantCommandValidator.Validate(command);
    if (errors.Count != 0) return Results.ValidationProblem(errors);

    var result = await handler.HandleAsync(command, cancellationToken);
    return result is null
        ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "Owner email is already in use.")
        : Results.Created($"/api/v1/tenants/{result.TenantId}", new ProvisionTenantResponse(
            result.TenantId, result.OwnerUserId, result.ProjectId, result.CreatedAt));
})
.WithName("ProvisionTenant")
.Produces<ProvisionTenantResponse>(StatusCodes.Status201Created)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status409Conflict);

app.Run();

public partial class Program;
