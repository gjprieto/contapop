using Contapop.Identity.Service.Infrastructure.Persistence;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Time;
using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Application.Commands.ProvisionTenant;
using Contapop.Identity.Service.Api.Contracts;
using Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddScoped<DomainEventOutboxInterceptor>();
builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity"))
        .AddInterceptors(serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>()));
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
    .AddCookie(IdentityConstants.ApplicationScheme);
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<IdentityCredential>, PasswordHasher<IdentityCredential>>();
builder.Services.AddSingleton<IClock, Contapop.Identity.Service.Infrastructure.Time.SystemClock>();
builder.Services.AddScoped<ProvisionTenantCommandHandler>();

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

public partial class Program;
