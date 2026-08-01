using System;
using System.Linq;
using System.Security.Cryptography;

namespace KamatekCrm.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = KamatekCrm.ApplicationCore.Security.UserPasswordPolicy.MinimumLength;

    public static string? Validate(string? password)
    {
        return KamatekCrm.ApplicationCore.Security.UserPasswordPolicy.Validate(password);
    }

    public static string GenerateTemporaryPassword(int length = 14)
    {
        length = Math.Max(length, MinimumLength);
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%*-_";
        const string alphabet = uppercase + lowercase + digits + symbols;

        var characters = new char[length];
        for (var index = 0; index < characters.Length; index++)
            characters[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        characters[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        characters[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        characters[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        characters[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        RandomNumberGenerator.Shuffle<char>(characters.AsSpan());
        return new string(characters);
    }
}
