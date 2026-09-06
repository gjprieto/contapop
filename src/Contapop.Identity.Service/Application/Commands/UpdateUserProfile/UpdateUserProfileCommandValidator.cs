namespace Contapop.Identity.Service.Application.Commands.UpdateUserProfile;

public static class UpdateUserProfileCommandValidator
{
    public static Dictionary<string, string[]> Validate(UpdateUserProfileCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (command.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name must be 200 characters or fewer."];
        }

        return errors;
    }
}
