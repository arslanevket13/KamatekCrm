using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.Infrastructure.Services;

internal sealed class DatabaseInitializationService : IDatabaseInitializationService
{
    private const string MigrationMutexName = "Global\\KamatekCrm_Database_Migration_Mutex";
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseInitializationService>? _logger;

    public DatabaseInitializationService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<DatabaseInitializationService>? logger = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<DatabaseInitializationResult> InitializeAsync(
        CancellationToken cancellationToken = default,
        bool seedDefaultAdminIfEmpty = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var migrationMutex = new Mutex(false, MigrationMutexName);
        bool hasHandle = false;

        try
        {
            try
            {
                var mutexTimeout = cancellationToken.CanBeCanceled ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
                hasHandle = migrationMutex.WaitOne(mutexTimeout);
                if (!hasHandle)
                {
                    _logger?.LogWarning("Veritabanı migration kilidi alınamadı; başka bir istemci migration çalıştırıyor olabilir.");
                }
            }
            catch (AbandonedMutexException)
            {
                hasHandle = true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            _logger?.LogInformation("Veritabanı şema uyumluluğu kontrol ediliyor...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger?.LogInformation("Veritabanı şeması güncel.");

            var userCount = await dbContext.Users.CountAsync(cancellationToken);
            if (userCount == 0)
            {
                if (!seedDefaultAdminIfEmpty)
                {
                    return new DatabaseInitializationResult(false, null, HasAnyUsers: false, UserCount: 0);
                }

                var temporaryPassword = GenerateTemporaryPassword();
                DbSeeder.SeedDemoData(dbContext, temporaryPassword);
                return new DatabaseInitializationResult(true, temporaryPassword, HasAnyUsers: true, UserCount: 1);
            }

            return new DatabaseInitializationResult(false, null, HasAnyUsers: true, UserCount: userCount);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Veritabanı kurulum/şema kontrolü iptal edildi veya zaman aşımına uğradı.");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Veritabanı otomatik kurulum veya şema güncelleme hatası.");
            throw new Exception("Veritabanı güncellenirken bir sorun oluştu. Sistem bakım modunda. Lütfen sistem yöneticinizle iletişime geçin.", ex);
        }
        finally
        {
            if (hasHandle)
            {
                try
                {
                    migrationMutex.ReleaseMutex();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Veritabanı migration mutex serbest bırakılırken önemsiz hata.");
                }
            }
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var characters = new char[16];
        for (var i = 0; i < characters.Length; i++)
            characters[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        characters[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        characters[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        characters[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        characters[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        RandomNumberGenerator.Shuffle<char>(characters.AsSpan());
        return new string(characters);
    }
}
