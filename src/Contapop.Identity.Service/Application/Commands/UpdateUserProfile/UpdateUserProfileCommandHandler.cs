using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Identity.Service.Application.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler(IdentityDbContext database, IClock clock)
{
    public async Task<UpdateUserProfileResult?> HandleAsync(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        if (UpdateUserProfileCommandValidator.Validate(command).Count != 0)
        {
            throw new ArgumentException("UpdateUserProfile command is invalid.", nameof(command));
        }

        var user = await database.DomainUsers.SingleOrDefaultAsync(
            candidate => candidate.Id == command.UserId && candidate.TenantId == command.TenantId,
            cancellationToken);
        if (user is null || user.Version != command.ExpectedVersion)
        {
            return null;
        }

        user.UpdateProfile(command.Name.Trim(), clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        return new UpdateUserProfileResult(user.Id, user.Name, user.UpdatedAt, user.Version);
    }
}
