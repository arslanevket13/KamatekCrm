namespace KamatekCrm.Shared.Enums
{
    /// <summary>
    /// İletişim kanalları
    /// </summary>
    public enum InteractionChannel
    {
        Phone = 1,
        InPerson = 2,
        Email = 3,
        WhatsApp = 4,
        Web = 5,
        Other = 99
    }

    /// <summary>
    /// Müşteri talep türleri
    /// </summary>
    public enum InteractionRequestType
    {
        PriceQuote = 1,         // Fiyat / Teklif talebi
        Discovery = 2,          // Keşif talebi
        ServiceStatus = 3,      // Servis durumu sorgulama
        Complaint = 4,          // Şikayet
        CallBack = 5,           // Geri aranma talebi
        ManagerAgenda = 6,      // Patron / Yönetici ile görüşme
        TechnicalSupport = 7,   // Teknik destek
        Payment = 8,            // Fatura / Ödeme / Tahsilat
        Appointment = 9,        // Randevu
        Other = 99              // Diğer
    }

    /// <summary>
    /// Görüşme öncelik seviyeleri
    /// </summary>
    public enum InteractionPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Görüşme ve talep iş durumları
    /// </summary>
    public enum InteractionStatus
    {
        New = 1,                // Yeni kayıt
        Seen = 2,               // Görüldü / İncelendi
        Assigned = 3,           // Sorumluya atandı
        InProgress = 4,         // İşlemde
        WaitingCustomer = 5,    // Müşteriden bilgi bekleniyor
        WaitingManager = 6,     // Yönetici onayı/ilgisi bekleniyor
        Scheduled = 7,          // Takip tarihi planlandı
        Completed = 8,          // Tamamlandı / Kapandı
        Cancelled = 9,          // İptal edildi
        Overdue = 10            // Gecikmiş takip
    }
}
