namespace KamatekCrm.Enums
{
    /// <summary>
    /// Servis işi durum filtresi
    /// </summary>
    public enum StatusFilter
    {
        /// <summary>
        /// Tüm işler
        /// </summary>
        Tümü = 0,

        /// <summary>
        /// Bekleyen işler
        /// </summary>
        Bekleyen = 1,

        /// <summary>
        /// Devam eden işler
        /// </summary>
        DevamEden = 2,

        /// <summary>
        /// Tamamlanan işler
        /// </summary>
        Tamamlanan = 3
    }
}
