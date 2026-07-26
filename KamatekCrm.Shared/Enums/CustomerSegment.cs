namespace KamatekCrm.Shared.Enums
{
    /// <summary>
    /// Müşteri segmentasyonu
    /// </summary>
    public enum CustomerSegment
    {
        /// <summary>
        /// Segmentsiz
        /// </summary>
        None = 0,

        /// <summary>
        /// VIP müşteri
        /// </summary>
        VIP = 1,

        /// <summary>
        /// Potansiyel müşteri
        /// </summary>
        Potential = 2,

        /// <summary>
        /// Riskli müşteri (borçlu, şikayetçi)
        /// </summary>
        AtRisk = 3,

        /// <summary>
        /// Pasif müşteri (uzun süre işlem yapmadı)
        /// </summary>
        Passive = 4,

        /// <summary>
        /// Yeni müşteri
        /// </summary>
        New = 5
    }
}
