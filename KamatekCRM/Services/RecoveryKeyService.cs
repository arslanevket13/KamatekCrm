using System;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Settings;

namespace KamatekCrm.Services
{
    public sealed class RecoveryKeyService : IRecoveryKeyService
    {
        private const string HashPrefix = "v1";
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;
        private const int Pbkdf2Iterations = 100_000;
        private static readonly char[] KeyAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        public string GenerateRecoveryKey()
        {
            return $"KMTK-{GenerateSegment(4)}-{GenerateSegment(4)}-{GenerateSegment(4)}";
        }

        public string HashRecoveryKey(string recoveryKey)
        {
            if (string.IsNullOrWhiteSpace(recoveryKey))
                throw new ArgumentException("Kurtarma anahtarı boş olamaz.", nameof(recoveryKey));

            var normalized = NormalizeKey(recoveryKey);
            var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            var derived = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(normalized),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                HashSizeBytes);

            return $"{HashPrefix}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(derived)}";
        }

        public bool VerifyRecoveryKey(string candidateKey, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(candidateKey) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 3 || !string.Equals(parts[0], HashPrefix, StringComparison.Ordinal))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expectedHash = Convert.FromBase64String(parts[2]);
                var normalized = NormalizeKey(candidateKey);

                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(normalized),
                    salt,
                    Pbkdf2Iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public string GenerateAndStoreNewRecoveryKey()
        {
            var key = GenerateRecoveryKey();
            AppSettings.RecoveryKeyHash = HashRecoveryKey(key);
            return key;
        }

        public bool VerifyStoredRecoveryKey(string candidateKey)
        {
            return VerifyRecoveryKey(candidateKey, AppSettings.RecoveryKeyHash);
        }

        private static string GenerateSegment(int length)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = KeyAlphabet[RandomNumberGenerator.GetInt32(KeyAlphabet.Length)];
            }
            return new string(chars);
        }

        private static string NormalizeKey(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToUpperInvariant(ch));
                }
            }
            return sb.ToString();
        }
    }
}
