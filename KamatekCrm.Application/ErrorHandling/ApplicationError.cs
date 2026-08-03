using System;
using System.Collections.Generic;

namespace KamatekCrm.ApplicationCore.ErrorHandling;

/// <summary>
/// Uygulama katmanının UI tiplerinden bağımsız, güvenli, sınıflandırılmış ve izlenebilir hata modeli.
/// </summary>
public record ApplicationError
{
    public ErrorCategory Category { get; init; }
    public string UserMessage { get; init; }
    public string CorrelationId { get; init; }
    public string? Code { get; init; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public ApplicationError(
        ErrorCategory category,
        string userMessage,
        string? correlationId = null,
        string? code = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Category = category;
        UserMessage = string.IsNullOrWhiteSpace(userMessage) 
            ? GetDefaultUserMessage(category) 
            : userMessage.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? $"ERR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}[..8]"
            : correlationId;
        Code = code ?? category.ToString().ToUpperInvariant();
        ValidationErrors = validationErrors;
    }

    public bool IsCancellation => Category == ErrorCategory.Cancellation;

    public static string GetDefaultUserMessage(ErrorCategory category) => category switch
    {
        ErrorCategory.Validation => "Girdiğiniz verilerde doğrulama hatası bulundu. Lütfen alanları kontrol ediniz.",
        ErrorCategory.Authentication => "Kimlik doğrulaması başarısız. Lütfen tekrar giriş yapınız.",
        ErrorCategory.Authorization => "Bu işlemi gerçekleştirmek için gerekli yetkiniz bulunmamaktadır.",
        ErrorCategory.NotFound => "Aradığınız kayıt veya kaynak sistemde bulunamadı.",
        ErrorCategory.Conflict => "Bu işlem mevcut başka bir kayıt veya işlemle çakışmaktadır.",
        ErrorCategory.Concurrency => "Kaydedilmek istenen veri başka bir kullanıcı tarafından güncellenmiş. Lütfen sayfayı yenileyiniz.",
        ErrorCategory.DatabaseConnection => "Veritabanı sunucusuna erişilemiyor. Lütfen ağ bağlantınızı kontrol ediniz.",
        ErrorCategory.DatabaseConstraint => "Veri bütünlüğü ihlali nedeniyle işlem gerçekleştirilemedi.",
        ErrorCategory.Network => "Ağ veya sunucu iletişimi sırasında bir hata oluştu. Lütfen bağlantınızı kontrol ediniz.",
        ErrorCategory.FileSystem => "Dosya okuma veya yazma işlemi gerçekleştirilemedi.",
        ErrorCategory.Printing => "Yazıcı veya belge yazdırma sürücüsü yanıt vermiyor.",
        ErrorCategory.ExternalService => "Harici entegrasyon servisinden yanıt alınamadı.",
        ErrorCategory.Cancellation => "İşlem kullanıcı tarafından veya zaman aşımı nedeniyle iptal edildi.",
        _ => "Beklenmeyen bir sistem hatası oluştu. Lütfen sistem yöneticiniz ile iletişime geçiniz."
    };
}
