namespace PlateformeFormation.Api.Helpers;

public static class PasswordHelper
{
    public static (bool IsValid, string? Error) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return (false, "Password must be at least 8 characters.");
        if (!password.Any(char.IsUpper))
            return (false, "Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower))
            return (false, "Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit))
            return (false, "Password must contain at least one digit.");
        return (true, null);
    }
}
