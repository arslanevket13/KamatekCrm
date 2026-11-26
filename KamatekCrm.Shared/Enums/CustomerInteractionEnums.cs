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

    /// <summary>
    /// Görüşme / Vaka yönü
    /// </summary>
    public enum InteractionDirection
    {
        InboundRequest = 1,       // Müşteri Talebi (Müşteri Aradı/Geldi)
        OutboundInfoRequest = 2,  // Bilgi/Onay İstendi (Biz Aradık/Sorduk)
        InternalNote = 3          // İç Not
    }

    /// <summary>
    /// Vaka sorumluluk durumu (Top kimde)
    /// </summary>
    public enum BallInCourtStatus
    {
        Us = 1,         // Top Bizde
        Customer = 2,   // Müşteride
        Manager = 3     // Patronda
    }

    /// <summary>
    /// Çok katmanlı vaka zaman çizelgesi öğe türü
    /// </summary>
    public enum ThreadEntryType
    {
        Note = 1,
        CallAttempt = 2,
        CustomerReply = 3,
        Snooze = 4,
        Handoff = 5,
        ManagerInstruction = 6,
        SystemConversion = 7
    }
}
