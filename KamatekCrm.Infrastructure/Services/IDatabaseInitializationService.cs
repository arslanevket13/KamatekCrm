using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.Infrastructure.Services;

/// <summary>
/// Uygulamanın kullandığı veritabanını şema ve başlangıç verileri bakımından hazırlar.
/// UI katmanının EF Core veya sağlayıcıya özel DDL bilmesine engel olur.
/// </summary>
public interface IDatabaseInitializationService
{
    Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed record DatabaseInitializationResult(bool AdminCreated, string? TemporaryAdminPassword);
