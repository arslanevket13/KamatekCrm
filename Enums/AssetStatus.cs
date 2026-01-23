namespace KamatekCrm.Enums
{
    /// <summary>
    /// Müşteri cihaz durumu
    /// </summary>
    public enum AssetStatus
    {
        /// <summary>
        /// Aktif - Çalışıyor
        /// </summary>
        Active,

        /// <summary>
        /// Arızalı - Tamir gerekiyor
        /// </summary>
        NeedsRepair,

        /// <summary>
        /// Bakımda
        /// </summary>
        UnderMaintenance,

        /// <summary>
        /// Değiştirildi
        /// </summary>
        Replaced,

        /// <summary>
        /// Kullanım dışı
        /// </summary>
        Retired
    }
}
