namespace Contapop.Identity.Service.Application.Commands.ProvisionTenant;

public static class ProvisionTenantCommandValidator
{
    public static Dictionary<string, string[]> Validate(ProvisionTenantCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.TenantName)) errors["tenantName"] = ["Tenant name is required."];
        if (string.IsNullOrWhiteSpace(command.OwnerName)) errors["ownerName"] = ["Owner name is required."];
        if (string.IsNullOrWhiteSpace(command.OwnerEmail) || !command.OwnerEmail.Contains('@')) errors["ownerEmail"] = ["A valid owner email is required."];
        if (string.IsNullOrWhiteSpace(command.InitialPassword) || command.InitialPassword.Length < 8) errors["initialPassword"] = ["Initial password must be at least 8 characters."];
        return errors;
    }
}
