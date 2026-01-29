namespace KamatekCrm.Enums
{
    /// <summary>
    /// Satın alma durumu
    /// </summary>
    public enum PurchaseStatus
    {
        /// <summary>
        /// Sipariş bekliyor
        /// </summary>
        Pending,

        /// <summary>
        /// Sipariş verildi
        /// </summary>
        Ordered,

        /// <summary>
        /// Kargoda
        /// </summary>
        Shipped,

        /// <summary>
        /// Teslim alındı
        /// </summary>
        Received,

        /// <summary>
        /// İptal edildi
        /// </summary>
        Cancelled,

        /// <summary>
        /// Stok girişi bekleniyor (Kısmi teslimat veya onay aşaması)
        /// </summary>
        WaitingInventoryEntry,

        /// <summary>
        /// Tamamlandı (Stok işlendi, süreç bitti)
        /// </summary>
        Completed
    }
}
