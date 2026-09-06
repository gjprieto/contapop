using Microsoft.AspNetCore.Identity;

namespace Contapop.Identity.Service.Infrastructure.Identity;

public sealed class IdentityCredential : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public Guid DomainUserId { get; set; }
}
