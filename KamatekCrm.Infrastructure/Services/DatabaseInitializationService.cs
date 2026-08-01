using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

internal sealed class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DatabaseInitializationService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            var temporaryPassword = GenerateTemporaryPassword();
            DbSeeder.SeedDemoData(dbContext, temporaryPassword);
            return new DatabaseInitializationResult(true, temporaryPassword);
        }

        return new DatabaseInitializationResult(false, null);
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

        // Politika kategorilerinin tamamını garanti et.
        characters[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        characters[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        characters[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        characters[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        RandomNumberGenerator.Shuffle<char>(characters.AsSpan());
        return new string(characters);
    }
}
