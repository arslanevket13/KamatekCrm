namespace KamatekCrm.Enums
{
    /// <summary>
    /// Proje yapı tipi
    /// </summary>
    public enum StructureType
    {
        /// <summary>
        /// Tek birim (Villa, Dükkan, Müstakil)
        /// </summary>
        SingleUnit = 0,

        /// <summary>
        /// Apartman (Kat + Daire yapısı)
        /// </summary>
        Apartment = 1,

        /// <summary>
        /// Site (Blok + Daire yapısı)
        /// </summary>
        Site = 2,

        /// <summary>
        /// Fabrika/Ticari (Özel Bölgeler)
        /// </summary>
        Commercial = 3
    }
}
