namespace KamatekCrm.Enums
{
    /// <summary>
    /// Sipariş Durumu
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,    // Bekliyor
        Completed = 1,  // Tamamlandı
        Cancelled = 2,  // İptal
        Refunded = 3    // İade
    }
}
