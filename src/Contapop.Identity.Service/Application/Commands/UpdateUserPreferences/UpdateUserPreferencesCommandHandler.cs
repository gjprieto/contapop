using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Identity.Service.Application.Commands.UpdateUserPreferences;

public sealed class UpdateUserPreferencesCommandHandler(IdentityDbContext database, IClock clock)
{
    public async Task<UpdateUserPreferencesResult?> HandleAsync(UpdateUserPreferencesCommand command, CancellationToken cancellationToken)
    {
        if (UpdateUserPreferencesCommandValidator.Validate(command).Count != 0)
        {
            throw new ArgumentException("UpdateUserPreferences command is invalid.", nameof(command));
        }

        var user = await database.DomainUsers.SingleOrDefaultAsync(
            candidate => candidate.Id == command.UserId && candidate.TenantId == command.TenantId,
            cancellationToken);
        if (user is null || user.Version != command.ExpectedVersion)
        {
            return null;
        }

        user.UpdatePreferences(
            command.Theme,
            command.Language?.Trim(),
            command.NotificationsEnabled,
            clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        return new UpdateUserPreferencesResult(
            user.Id,
            user.Theme,
            user.Language,
            user.NotificationsEnabled,
            user.UpdatedAt,
            user.Version);
    }
}
