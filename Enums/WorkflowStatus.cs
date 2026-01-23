namespace KamatekCrm.Enums
{
    /// <summary>
    /// Proje iş akışı durumu (5 fazlı yaşam döngüsü)
    /// </summary>
    public enum WorkflowStatus
    {
        /// <summary>
        /// Taslak - Keşif aşamasında
        /// </summary>
        Draft = 0,

        /// <summary>
        /// Teklif Gönderildi - Proforma müşteriye iletildi
        /// </summary>
        ProposalSent = 1,

        /// <summary>
        /// Onaylandı - Müşteri teklifi kabul etti
        /// </summary>
        Approved = 2,

        /// <summary>
        /// Planlandı - Teknisyen ve tarih atandı
        /// </summary>
        Scheduled = 3,

        /// <summary>
        /// Devam Ediyor - İş başladı
        /// </summary>
        InProgress = 4,

        /// <summary>
        /// Parça Bekleniyor - Tedarik bekleniyor
        /// </summary>
        WaitingForParts = 5,

        /// <summary>
        /// Final İnceleme - Tahmini vs Gerçek onayı
        /// </summary>
        PendingFinalReview = 6,

        /// <summary>
        /// Tamamlandı - Stok düşüldü, fatura kesildi
        /// </summary>
        Completed = 7,

        /// <summary>
        /// İptal Edildi
        /// </summary>
        Cancelled = 8
    }
}
