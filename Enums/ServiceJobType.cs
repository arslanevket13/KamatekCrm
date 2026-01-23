namespace KamatekCrm.Enums
{
    /// <summary>
    /// Servis işi tipi - Arıza vs Proje ayrımı
    /// </summary>
    public enum ServiceJobType
    {
        /// <summary>
        /// Arıza/Servis - Hızlı müdahale gerektiren işler
        /// </summary>
        Fault = 0,

        /// <summary>
        /// Proje/Kurulum - Keşif → Teklif → Onay → Uygulama → Final akışı
        /// </summary>
        Project = 1
    }
}
