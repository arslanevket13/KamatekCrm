namespace KamatekCrm.Enums
{
    /// <summary>
    /// Stok Hareket Tipleri
    /// </summary>
    public enum StockTransactionType
    {
        /// <summary>
        /// Satınalma / Giriş
        /// </summary>
        Purchase = 0,

        /// <summary>
        /// Satış / Çıkış
        /// </summary>
        Sale = 1,

        /// <summary>
        /// Servis/İş Kullanımı (Tüketim)
        /// </summary>
        ServiceUsage = 2,

        /// <summary>
        /// Depolar Arası Transfer
        /// </summary>
        Transfer = 3,

        /// <summary>
        /// Sayım Fazlası (Stok Artışı)
        /// </summary>
        AdjustmentPlus = 4,

        /// <summary>
        /// Sayım Eksiği (Stok Azalışı/Zayi)
        /// </summary>
        AdjustmentMinus = 5,

        /// <summary>
        /// Tedarikçiye İade (Çıkış)
        /// </summary>
        ReturnToSupplier = 6,

        /// <summary>
        /// Müşteriden İade (Giriş)
        /// </summary>
        ReturnFromCustomer = 7,

        /// <summary>
        /// Açılış Stoğu (İlk Giriş)
        /// </summary>
        OpeningStock = 8
    }
}
