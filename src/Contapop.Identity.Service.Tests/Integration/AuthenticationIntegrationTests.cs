using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Contapop.Identity.Service.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace Contapop.Identity.Service.Tests.Integration;

public sealed class AuthenticationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    [Fact]
    public async Task Login_and_change_password_use_the_provisioned_identity_credential()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        var email = "ana@acme.test";
        const string originalPassword = "OriginalPassword";
        const string newPassword = "NewPassword";

        var provision = await client.PostAsJsonAsync("/api/v1/tenants", new ProvisionTenantRequest(
            "Acme Studio", "Ana Garcia", email, originalPassword));
        Assert.Equal(HttpStatusCode.Created, provision.StatusCode);

        var failedLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "incorrect"));
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, originalPassword));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var changePassword = await client.PostAsJsonAsync("/api/v1/users/me/change-password", new ChangePasswordRequest(originalPassword, newPassword));
        Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);

        using var freshClient = _factory.CreateClient();
        var oldPasswordLogin = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, originalPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.NoContent, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Get_current_user_accepts_a_valid_internal_jwt_and_returns_the_claim_scoped_user()
    {
        using var client = _factory.CreateClient();
        var email = "maria@acme.test";
        var provision = await client.PostAsJsonAsync("/api/v1/tenants", new ProvisionTenantRequest(
            "Acme Studio", "Maria Garcia", email, "OriginalPassword"));
        var provisioned = await provision.Content.ReadFromJsonAsync<ProvisionTenantResponse>();

        Assert.NotNull(provisioned);

        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateInternalJwt(provisioned.TenantId, provisioned.OwnerUserId));
        var response = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(user);
        Assert.Equal(provisioned.OwnerUserId, user.UserId);
        Assert.Equal(provisioned.TenantId, user.TenantId);
        Assert.Equal(provisioned.ProjectId, user.ProjectId);
        Assert.Equal(1, user.Version);
    }

    private static string CreateInternalJwt(Guid tenantId, Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-internal-jwt-signing-key-not-for-production-2026")),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("user_id", userId.ToString()),
                new Claim(ClaimTypes.Role, "account-owner"),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:identity"] = _postgres.GetConnectionString(),
                    ["InternalJwt:SigningKey"] = "test-internal-jwt-signing-key-not-for-production-2026",
                })));
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }
}
