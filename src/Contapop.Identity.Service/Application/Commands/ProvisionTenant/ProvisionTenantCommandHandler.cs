using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Domain.Tenancy;
using Contapop.Identity.Service.Domain.Users;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Outbox;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Identity.Service.Application.Commands.ProvisionTenant;

public sealed class ProvisionTenantCommandHandler(
    IdentityDbContext database,
    IPasswordHasher<IdentityCredential> passwordHasher,
    IClock clock)
{
    public async Task<ProvisionTenantResult?> HandleAsync(
        ProvisionTenantCommand command,
        CancellationToken cancellationToken)
    {
        if (ProvisionTenantCommandValidator.Validate(command).Count != 0)
        {
            throw new ArgumentException("ProvisionTenant command is invalid.", nameof(command));
        }

        var normalizedEmail = command.OwnerEmail.Trim().ToUpperInvariant();
        if (await database.DomainUsers.AnyAsync(user => user.Email.ToUpper() == normalizedEmail, cancellationToken))
        {
            return null;
        }

        var createdAt = clock.UtcNow;
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var tenant = Tenant.Create(tenantId, command.TenantName.Trim(), createdAt);
        var owner = User.Create(ownerUserId, tenantId, command.OwnerName.Trim(), command.OwnerEmail.Trim(), createdAt);
        var project = Project.Create(projectId, tenantId, command.TenantName.Trim(), createdAt);
        tenant.MarkProvisioned(ownerUserId);
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DomainUserId = ownerUserId,
            UserName = command.OwnerEmail.Trim(),
            NormalizedUserName = normalizedEmail,
            Email = command.OwnerEmail.Trim(),
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
        };
        credential.PasswordHash = passwordHasher.HashPassword(credential, command.InitialPassword);

        database.Tenants.Add(tenant);
        database.DomainUsers.Add(owner);
        database.Projects.Add(project);
        database.Set<IdentityCredential>().Add(credential);
        await database.SaveChangesAsync(cancellationToken);
        return new ProvisionTenantResult(tenantId, ownerUserId, projectId, createdAt);
    }
}
