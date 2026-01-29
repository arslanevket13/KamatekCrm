namespace KamatekCrm.Enums
{
    /// <summary>
    /// Tedarikçi Tipi - Firma türüne göre sınıflandırma
    /// </summary>
    public enum SupplierType
    {
        /// <summary>
        /// Toptancı - Toptan ürün tedarikçisi
        /// </summary>
        Wholesaler = 0,

        /// <summary>
        /// Servis Sağlayıcı - Teknik servis hizmeti
        /// </summary>
        ServiceProvider = 1,

        /// <summary>
        /// Üretici - Ürün imalatçısı
        /// </summary>
        Manufacturer = 2,

        /// <summary>
        /// Distribütör - Bölge dağıtıcısı
        /// </summary>
        Distributor = 3,

        /// <summary>
        /// Diğer
        /// </summary>
        Other = 4
    }
}
