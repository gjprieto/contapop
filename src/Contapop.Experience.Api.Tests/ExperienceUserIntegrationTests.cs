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

    [Fact]
    public async Task Update_user_profile_forwards_the_internal_jwt_and_version()
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/experience/v1/user")
        {
            Content = JsonContent.Create(new UpdateUserProfileRequest("Ana Lopez")),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UpdateUserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Ana Lopez", profile.Name);
        Assert.Equal(2, profile.Version);

        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/experience/v1/user");
        Assert.NotNull(user);
        Assert.Equal("Ana Lopez", user.Name);
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
        var currentUser = new CurrentUserResponse(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Ana Garcia", "ana@acme.test", "light", "es", true, 1);
        _identityService.MapGet("/api/v1/users/me", (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("11111111-1111-1111-1111-111111111111", context.User.FindFirstValue("tenant_id"));
            Assert.Equal("22222222-2222-2222-2222-222222222222", context.User.FindFirstValue("user_id"));
            Assert.Equal("account-owner", context.User.FindFirstValue(ClaimTypes.Role));
            return Results.Ok(currentUser);
        }).RequireAuthorization();
        _identityService.MapPatch("/api/v1/users/me/profile", async (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("\"1\"", context.Request.Headers.IfMatch.ToString());
            var request = await context.Request.ReadFromJsonAsync<UpdateUserProfileRequest>();
            Assert.NotNull(request);
            currentUser = currentUser with { Name = request.Name, Version = 2 };
            return Results.Ok(new UpdateUserProfileResponse(currentUser.UserId, currentUser.Name,
                new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), currentUser.Version));
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
