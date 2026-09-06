namespace Contapop.Identity.Service.Application.Commands.UpdateUserPreferences;

public static class UpdateUserPreferencesCommandValidator
{
    private static readonly HashSet<string> Themes = ["light", "dark"];

    public static Dictionary<string, string[]> Validate(UpdateUserPreferencesCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (command.Theme is null && command.Language is null && command.NotificationsEnabled is null)
        {
            errors["request"] = ["At least one preference must be supplied."];
        }

        if (command.Theme is not null && !Themes.Contains(command.Theme))
        {
            errors["theme"] = ["Theme must be either 'light' or 'dark'."];
        }

        if (command.Language is not null && (string.IsNullOrWhiteSpace(command.Language) || command.Language.Length > 10))
        {
            errors["language"] = ["Language must be between 1 and 10 characters."];
        }

        return errors;
    }
}
