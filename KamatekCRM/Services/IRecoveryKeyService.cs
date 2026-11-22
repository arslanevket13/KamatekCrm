namespace KamatekCrm.Services
{
    /// <summary>
    /// Çevrimdışı yönetici şifre sıfırlama için tek kullanımlık kurtarma anahtarı üretimi ve sabit zamanlı doğrulaması.
    /// </summary>
    public interface IRecoveryKeyService
    {
        string GenerateRecoveryKey();

        string HashRecoveryKey(string recoveryKey);

        bool VerifyRecoveryKey(string candidateKey, string? storedHash);

        string GenerateAndStoreNewRecoveryKey();

        bool VerifyStoredRecoveryKey(string candidateKey);
    }
}
