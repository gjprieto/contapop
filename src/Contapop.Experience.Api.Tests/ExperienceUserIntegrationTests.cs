using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Contapop.Experience.Api.Tests;

public sealed class ExperienceUserIntegrationTests : IAsyncLifetime
{
    private const string SigningKey = "test-internal-jwt-signing-key-not-for-production-2026";
    private WebApplication _identityService = null!;
    private WebApplicationFactory<Program> _experienceFactory = null!;

    [Fact]
    public async Task Login_and_get_user_send_a_signed_internal_jwt_to_identity()
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/experience/v1/user");

        Assert.NotNull(user);
        Assert.Equal("Ana Garcia", user.Name);
    }

    public async Task InitializeAsync()
    {
        var identityBuilder = WebApplication.CreateBuilder();
        identityBuilder.WebHost.UseTestServer();
        identityBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(SigningKey)),
                };
            });
        identityBuilder.Services.AddAuthorization();
        _identityService = identityBuilder.Build();
        _identityService.UseAuthentication();
        _identityService.UseAuthorization();
        _identityService.MapPost("/api/v1/auth/validate-credentials", () =>
            Results.Ok(new AuthenticatedUserResponse(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), "account-owner")));
        _identityService.MapGet("/api/v1/users/me", (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("11111111-1111-1111-1111-111111111111", context.User.FindFirstValue("tenant_id"));
            Assert.Equal("22222222-2222-2222-2222-222222222222", context.User.FindFirstValue("user_id"));
            Assert.Equal("account-owner", context.User.FindFirstValue(ClaimTypes.Role));
            return Results.Ok(new CurrentUserResponse(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "Ana Garcia", "ana@acme.test", "light", "es", true, 1));
        }).RequireAuthorization();
        await _identityService.StartAsync();

        _experienceFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["InternalJwt:SigningKey"] = SigningKey }));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient("identity-service")
                    .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://identity-service"))
                    .ConfigurePrimaryHttpMessageHandler(() => _identityService.GetTestServer().CreateHandler()));
        });
    }

    public async Task DisposeAsync()
    {
        _experienceFactory.Dispose();
        await _identityService.DisposeAsync();
    }
}
