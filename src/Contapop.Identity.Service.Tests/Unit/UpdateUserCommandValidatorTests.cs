using Contapop.Identity.Service.Application.Commands.UpdateUserPreferences;
using Contapop.Identity.Service.Application.Commands.UpdateUserProfile;

namespace Contapop.Identity.Service.Tests.Unit;

public sealed class UpdateUserCommandValidatorTests
{
    [Fact]
    public void Profile_validator_rejects_an_empty_name()
    {
        var result = UpdateUserProfileCommandValidator.Validate(new UpdateUserProfileCommand(Guid.NewGuid(), Guid.NewGuid(), 1, " "));

        Assert.Contains("name", result.Keys);
    }

    [Fact]
    public void Preferences_validator_rejects_a_request_without_preferences()
    {
        var result = UpdateUserPreferencesCommandValidator.Validate(
            new UpdateUserPreferencesCommand(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null));

        Assert.Contains("request", result.Keys);
    }

    [Fact]
    public void Preferences_validator_rejects_an_unknown_theme()
    {
        var result = UpdateUserPreferencesCommandValidator.Validate(
            new UpdateUserPreferencesCommand(Guid.NewGuid(), Guid.NewGuid(), 1, "blue", null, null));

        Assert.Contains("theme", result.Keys);
    }
}
