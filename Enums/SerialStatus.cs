namespace KamatekCrm.Enums
{
    /// <summary>
    /// Seri Numarası Durumları
    /// </summary>
    public enum SerialStatus
    {
        /// <summary>
        /// Kullanılabilir / Stokta
        /// </summary>
        Available = 0,

        /// <summary>
        /// Satıldı
        /// </summary>
        Sold = 1,

        /// <summary>
        /// Serviste Kullanıldı
        /// </summary>
        UsedInService = 2,

        /// <summary>
        /// Arızalı / Bozuk
        /// </summary>
        Defective = 3,
        
        /// <summary>
        /// İade Edildi
        /// </summary>
        Returned = 4
    }
}
