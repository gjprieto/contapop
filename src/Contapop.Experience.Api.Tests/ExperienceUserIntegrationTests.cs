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
    private WebApplication _ledgerService = null!;
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

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var profile = await response.Content.ReadFromJsonAsync<UpdateUserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Ana Lopez", profile.Name);
        Assert.Equal(2, profile.Version);

        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/experience/v1/user");
        Assert.NotNull(user);
        Assert.Equal("Ana Lopez", user.Name);
    }

    [Fact]
    public async Task Ledger_requests_forward_the_internal_jwt_and_required_command_headers()
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/experience/v1/transactions/44444444-4444-4444-4444-444444444444/archive");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "55555555-5555-5555-5555-555555555555");
        request.Headers.TryAddWithoutValidation("If-Match", "\"3\"");
        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Transaction_import_forwards_the_multipart_form_to_ledger()
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var content = new MultipartFormDataContent
        {
            { new StringContent("44444444-4444-4444-4444-444444444444"), "bankAccountId" },
            { new StringContent("{\"dateColumn\":\"Date\",\"amountColumn\":\"Amount\"}"), "columnMapping" },
            { new ByteArrayContent("Date,Amount\n2026-09-01,-24.50"u8.ToArray()), "file", "transactions.csv" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/experience/v1/transactions/import") { Content = content };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "55555555-5555-5555-5555-555555555555");

        var response = await client.SendAsync(request);

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseContent);
    }

    [Theory]
    [InlineData("/experience/v1/financial-overview/bank-accounts?status=active")]
    [InlineData("/experience/v1/financial-overview/payment-cards?page=2")]
    [InlineData("/experience/v1/transactions?search=rent&sort=date-desc")]
    [InlineData("/experience/v1/transactions/44444444-4444-4444-4444-444444444444")]
    public async Task Ledger_query_routes_forward_the_internal_jwt(string path)
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var response = await client.GetAsync(path);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/experience/v1/financial-overview/bank-accounts", "{\"projectId\":\"33333333-3333-3333-3333-333333333333\",\"accountNumber\":\"ES12\",\"bankName\":\"Banco Uno\"}")]
    [InlineData("/experience/v1/financial-overview/payment-cards", "{\"projectId\":\"33333333-3333-3333-3333-333333333333\",\"label\":\"Visa ending 1234\",\"cardholderName\":\"Ana Garcia\"}")]
    [InlineData("/experience/v1/transactions", "{\"bankAccountId\":\"44444444-4444-4444-4444-444444444444\",\"amountMinor\":1250,\"date\":\"2026-09-01\",\"type\":\"expense\"}")]
    [InlineData("/experience/v1/transactions/44444444-4444-4444-4444-444444444444", "{\"description\":\"Updated rent\"}")]
    public async Task Ledger_command_routes_forward_the_internal_jwt_and_idempotency_key(string path, string body)
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var request = new HttpRequestMessage(path.EndsWith("44444444-4444-4444-4444-444444444444", StringComparison.Ordinal) ? HttpMethod.Patch : HttpMethod.Post, path)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "55555555-5555-5555-5555-555555555555");
        if (request.Method == HttpMethod.Patch) request.Headers.TryAddWithoutValidation("If-Match", "\"3\"");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/experience/v1/financial-overview/bank-accounts/44444444-4444-4444-4444-444444444444/archive", "POST")]
    [InlineData("/experience/v1/financial-overview/payment-cards/44444444-4444-4444-4444-444444444444", "DELETE")]
    public async Task Ledger_account_lifecycle_routes_forward_the_required_command_headers(string path, string method)
    {
        using var client = _experienceFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/experience/v1/auth/login", new LoginRequest("ana@acme.test", "Password1"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "55555555-5555-5555-5555-555555555555");
        request.Headers.TryAddWithoutValidation("If-Match", "\"3\"");
        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
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

        var ledgerBuilder = WebApplication.CreateBuilder();
        ledgerBuilder.WebHost.UseTestServer();
        ledgerBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        ledgerBuilder.Services.AddAuthorization();
        _ledgerService = ledgerBuilder.Build();
        _ledgerService.UseAuthentication();
        _ledgerService.UseAuthorization();
        _ledgerService.MapPost("/api/v1/transactions/{transactionId:guid}/archive", (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("55555555-5555-5555-5555-555555555555", context.Request.Headers["Idempotency-Key"].ToString());
            Assert.Equal("\"3\"", context.Request.Headers.IfMatch.ToString());
            return Results.Ok(new { archived = true });
        }).RequireAuthorization();
        _ledgerService.MapPost("/api/v1/transactions/import", async (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("55555555-5555-5555-5555-555555555555", context.Request.Headers["Idempotency-Key"].ToString());
            var form = await context.Request.ReadFormAsync();
            Assert.Equal("44444444-4444-4444-4444-444444444444", form["bankAccountId"].ToString());
            Assert.Equal("transactions.csv", form.Files.GetFile("file")?.FileName);
            return Results.Ok(new { importedCount = 1 });
        }).RequireAuthorization();
        _ledgerService.MapMethods("/{**path}", ["GET", "POST", "PATCH", "DELETE"], (HttpContext context) =>
        {
            Assert.True(context.User.Identity?.IsAuthenticated);
            Assert.Equal("11111111-1111-1111-1111-111111111111", context.User.FindFirstValue("tenant_id"));
            if (context.Request.Method != HttpMethods.Get)
            {
                Assert.Equal("55555555-5555-5555-5555-555555555555", context.Request.Headers["Idempotency-Key"].ToString());
            }
            if (context.Request.Method is "PATCH" or "DELETE" || context.Request.Path.Value?.EndsWith("/archive", StringComparison.Ordinal) == true)
            {
                Assert.Equal("\"3\"", context.Request.Headers.IfMatch.ToString());
            }
            return Results.Ok(new { forwarded = true });
        }).RequireAuthorization();
        await _ledgerService.StartAsync();

        _experienceFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["InternalJwt:SigningKey"] = SigningKey }));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient("identity-service")
                    .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://identity-service"))
                    .ConfigurePrimaryHttpMessageHandler(() => _identityService.GetTestServer().CreateHandler())
                    .Services.AddHttpClient("ledger-service")
                    .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://ledger-service"))
                    .ConfigurePrimaryHttpMessageHandler(() => _ledgerService.GetTestServer().CreateHandler()));
        });
    }

    public async Task DisposeAsync()
    {
        _experienceFactory.Dispose();
        await _identityService.DisposeAsync();
        await _ledgerService.DisposeAsync();
    }
}
