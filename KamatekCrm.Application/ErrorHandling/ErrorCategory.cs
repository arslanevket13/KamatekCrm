namespace KamatekCrm.ApplicationCore.ErrorHandling;

/// <summary>
/// Sistem genelindeki tüm teknik ve iş mantığı hatalarının merkezi kategorileri.
/// </summary>
public enum ErrorCategory
{
    /// <summary> Girdi doğrulama ve veri formatı hataları </summary>
    Validation,

    /// <summary> Kimlik doğrulama ve oturum açma hataları </summary>
    Authentication,

    /// <summary> Yetkisiz erişim ve rol izni eksiklikleri </summary>
    Authorization,

    /// <summary> Kaynak veya veri bulunamadı hataları </summary>
    NotFound,

    /// <summary> Çakışan veri veya mükerrer kayıt durumları </summary>
    Conflict,

    /// <summary> Eşzamanlı erişim ve paralel güncelleme çakışmaları (Lock/Optimistic Concurrency) </summary>
    Concurrency,

    /// <summary> Veritabanı sunucusuna erişim ve bağlantı kopma hataları </summary>
    DatabaseConnection,

    /// <summary> Veritabanı kısıt (Unique, FK, Check constraint) ihlalleri </summary>
    DatabaseConstraint,

    /// <summary> Ağ, HTTP isteği veya soket seviyesindeki iletişim hataları </summary>
    Network,

    /// <summary> Dosya sistemi, I/O ve dizin erişim hataları </summary>
    FileSystem,

    /// <summary> Yazıcı ve belge yazdırma sürücü/donanım hataları </summary>
    Printing,

    /// <summary> Harici servis entegrasyonu ve uzak API hataları </summary>
    ExternalService,

    /// <summary> Kullanıcı iptali veya zaman aşımı nedeniyle durdurulan asenkron işlemler </summary>
    Cancellation,

    /// <summary> Beklenmeyen veya sınıflandırılamayan sistem hataları </summary>
    Unexpected
}
