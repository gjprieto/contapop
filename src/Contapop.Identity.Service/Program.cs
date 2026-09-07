using Contapop.Identity.Service.Infrastructure.Persistence;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Time;
using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Application.Commands.ProvisionTenant;
using Contapop.Identity.Service.Application.Commands.UpdateUserProfile;
using Contapop.Identity.Service.Application.Commands.UpdateUserPreferences;
using Contapop.Identity.Service.Api.Contracts;
using Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Dapr.Client;
using Contapop.Identity.Service.Infrastructure.Messaging;
using Contapop.Identity.Service.Infrastructure.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<DomainEventOutboxInterceptor>();
builder.Services.AddDbContextFactory<IdentityDbContext>((serviceProvider, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity"))
        .AddInterceptors(serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>()));
builder.Services.AddScoped<IdentityDbContext>(serviceProvider =>
    serviceProvider.GetRequiredService<IDbContextFactory<IdentityDbContext>>().CreateDbContext());
builder.Services.AddIdentityCore<IdentityCredential>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme)
    .AddJwtBearer("InternalJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["InternalJwt:SigningKey"] ?? throw new InvalidOperationException("Internal JWT signing key is not configured."))),
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("account-owner", policy =>
    {
        policy.AddAuthenticationSchemes("InternalJwt");
        policy.RequireRole("account-owner");
    }));
builder.Services.AddScoped<IPasswordHasher<IdentityCredential>, PasswordHasher<IdentityCredential>>();
builder.Services.AddSingleton<IClock, Contapop.Identity.Service.Infrastructure.Time.SystemClock>();
builder.Services.AddScoped<ProvisionTenantCommandHandler>();
builder.Services.AddScoped<UpdateUserProfileCommandHandler>();
builder.Services.AddScoped<UpdateUserPreferencesCommandHandler>();
builder.Services.AddSingleton<DaprClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var builder = new DaprClientBuilder();
    if (configuration["DAPR_GRPC_PORT"] is { Length: > 0 } grpcPort)
    {
        builder.UseGrpcEndpoint($"http://127.0.0.1:{grpcPort}");
    }

    return builder.Build();
});
builder.Services.AddScoped<IIntegrationEventPublisher, DaprIntegrationEventPublisher>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddHostedService<OutboxDispatchService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await database.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok());

app.MapPost("/api/v1/auth/login", async (
    LoginRequest request,
    UserManager<IdentityCredential> userManager,
    HttpContext httpContext) =>
{
    var credential = await userManager.FindByEmailAsync(request.Email.Trim());
    if (credential is null || !await userManager.CheckPasswordAsync(credential, request.Password))
    {
        return Results.Unauthorized();
    }

    var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, credential.Id.ToString()),
            new Claim("tenant_id", credential.TenantId.ToString()),
            new Claim("user_id", credential.DomainUserId.ToString()),
            new Claim(ClaimTypes.Role, "account-owner"),
        ],
        IdentityConstants.ApplicationScheme);
    await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));
    return Results.NoContent();
})
.AllowAnonymous()
.WithName("Login")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

app.MapPost("/api/v1/auth/validate-credentials", async (
    LoginRequest request,
    UserManager<IdentityCredential> userManager) =>
{
    var credential = await userManager.FindByEmailAsync(request.Email.Trim());
    if (credential is null || !await userManager.CheckPasswordAsync(credential, request.Password))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new AuthenticatedUserResponse(credential.TenantId, credential.DomainUserId, "account-owner"));
})
.AllowAnonymous()
.WithName("ValidateCredentials")
.Produces<AuthenticatedUserResponse>()
.Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/api/v1/users/me", async (
    HttpContext httpContext,
    IdentityDbContext database,
    CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(httpContext.User.FindFirstValue("tenant_id"), out var tenantId)
        || !Guid.TryParse(httpContext.User.FindFirstValue("user_id"), out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await database.DomainUsers.AsNoTracking()
        .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.TenantId == tenantId, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var projectId = await database.Projects.AsNoTracking()
        .Where(project => project.TenantId == tenantId && project.Status == "active")
        .Select(project => (Guid?)project.Id)
        .SingleOrDefaultAsync(cancellationToken);
    if (projectId is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "No active project exists for this tenant.");
    }

    return Results.Ok(new CurrentUserResponse(user.Id, user.TenantId, projectId.Value, user.Name, user.Email, user.Theme, user.Language, user.NotificationsEnabled, user.Version));
})
.RequireAuthorization("account-owner")
.WithName("GetCurrentUser")
.Produces<CurrentUserResponse>()
.Produces(StatusCodes.Status401Unauthorized);

app.MapPatch("/api/v1/users/me/profile", async (
    UpdateUserProfileRequest request,
    HttpContext httpContext,
    UpdateUserProfileCommandHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetCurrentUser(httpContext, out var tenantId, out var userId) || !TryGetExpectedVersion(httpContext, out var expectedVersion))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["If-Match"] = ["A quoted current resource version is required."],
        });
    }

    var command = new UpdateUserProfileCommand(tenantId, userId, expectedVersion, request.Name);
    var errors = UpdateUserProfileCommandValidator.Validate(command);
    if (errors.Count != 0)
    {
        return Results.ValidationProblem(errors);
    }

    var result = await handler.HandleAsync(command, cancellationToken);
    return result is null
        ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The user profile has changed. Refresh and try again.")
        : Results.Ok(result);
})
.RequireAuthorization("account-owner")
.WithName("UpdateUserProfile")
.Produces<UpdateUserProfileResult>()
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status409Conflict);

app.MapPatch("/api/v1/users/me/preferences", async (
    UpdateUserPreferencesRequest request,
    HttpContext httpContext,
    UpdateUserPreferencesCommandHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!TryGetCurrentUser(httpContext, out var tenantId, out var userId) || !TryGetExpectedVersion(httpContext, out var expectedVersion))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["If-Match"] = ["A quoted current resource version is required."],
        });
    }

    var command = new UpdateUserPreferencesCommand(tenantId, userId, expectedVersion, request.Theme, request.Language, request.NotificationsEnabled);
    var errors = UpdateUserPreferencesCommandValidator.Validate(command);
    if (errors.Count != 0)
    {
        return Results.ValidationProblem(errors);
    }

    var result = await handler.HandleAsync(command, cancellationToken);
    return result is null
        ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The user preferences have changed. Refresh and try again.")
        : Results.Ok(result);
})
.RequireAuthorization("account-owner")
.WithName("UpdateUserPreferences")
.Produces<UpdateUserPreferencesResult>()
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status409Conflict);

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

app.MapPost("/api/v1/users/me/change-password", async (
    ChangePasswordRequest request,
    UserManager<IdentityCredential> userManager,
    HttpContext httpContext) =>
{
    if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var credentialId))
    {
        return Results.Unauthorized();
    }

    var credential = await userManager.FindByIdAsync(credentialId.ToString());
    if (credential is null)
    {
        return Results.Unauthorized();
    }

    var result = await userManager.ChangePasswordAsync(credential, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
    {
        if (result.Errors.Any(error => error.Code == "PasswordMismatch"))
        {
            return Results.BadRequest(new { error = "Current password is incorrect." });
        }

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["newPassword"] = result.Errors.Select(error => error.Description).ToArray(),
        });
    }

    return Results.NoContent();
})
.RequireAuthorization()
.WithName("ChangePassword")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status400BadRequest)
.ProducesValidationProblem()
.Produces(StatusCodes.Status401Unauthorized);

app.Run();

static bool TryGetCurrentUser(HttpContext httpContext, out Guid tenantId, out Guid userId)
{
    var hasTenantId = Guid.TryParse(httpContext.User.FindFirstValue("tenant_id"), out tenantId);
    var hasUserId = Guid.TryParse(httpContext.User.FindFirstValue("user_id"), out userId);
    return hasTenantId && hasUserId;
}

static bool TryGetExpectedVersion(HttpContext httpContext, out int version)
{
    var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
    var parsedVersion = 0;
    var isValid = ifMatch.Length >= 3
        && ifMatch[0] == '"'
        && ifMatch[^1] == '"'
        && int.TryParse(ifMatch[1..^1], out parsedVersion)
        && parsedVersion > 0;
    version = isValid ? parsedVersion : 0;
    return isValid;
}

public partial class Program;

public sealed record AuthenticatedUserResponse(Guid TenantId, Guid UserId, string Role);
public sealed record CurrentUserResponse(Guid UserId, Guid TenantId, Guid ProjectId, string Name, string Email, string Theme, string Language, bool NotificationsEnabled, int Version);
