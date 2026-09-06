using System.Net;
using System.Net.Http.Json;
using Contapop.Identity.Service.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:identity"] = _postgres.GetConnectionString(),
                })));
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }
}
