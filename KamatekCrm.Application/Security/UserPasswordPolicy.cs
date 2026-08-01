namespace KamatekCrm.ApplicationCore.Security;

public static class UserPasswordPolicy
{
    public const int MinimumLength = 10;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
            return $"Şifre en az {MinimumLength} karakter olmalıdır.";
        if (!password.Any(char.IsUpper)) return "Şifre en az bir büyük harf içermelidir.";
        if (!password.Any(char.IsLower)) return "Şifre en az bir küçük harf içermelidir.";
        if (!password.Any(char.IsDigit)) return "Şifre en az bir rakam içermelidir.";
        if (!password.Any(character => !char.IsLetterOrDigit(character)))
            return "Şifre en az bir özel karakter içermelidir.";

        return null;
    }
}
