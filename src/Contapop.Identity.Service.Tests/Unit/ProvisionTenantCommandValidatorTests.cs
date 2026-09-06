using Contapop.Identity.Service.Application.Commands.ProvisionTenant;

namespace Contapop.Identity.Service.Tests.Unit;

public sealed class ProvisionTenantCommandValidatorTests
{
    [Fact]
    public void Validate_returns_errors_for_missing_required_values()
    {
        var errors = ProvisionTenantCommandValidator.Validate(new("", "", "not-an-email", "short"));

        Assert.Contains("tenantName", errors.Keys);
        Assert.Contains("ownerName", errors.Keys);
        Assert.Contains("ownerEmail", errors.Keys);
        Assert.Contains("initialPassword", errors.Keys);
    }
}
