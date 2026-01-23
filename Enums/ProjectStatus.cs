namespace KamatekCrm.Enums
{
    /// <summary>
    /// Proje durumu
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// Taslak
        /// </summary>
        Draft,

        /// <summary>
        /// Onay bekliyor
        /// </summary>
        PendingApproval,

        /// <summary>
        /// Devam ediyor
        /// </summary>
        Active,

        /// <summary>
        /// Beklemede
        /// </summary>
        OnHold,

        /// <summary>
        /// Tamamlandı
        /// </summary>
        Completed,

        /// <summary>
        /// İptal edildi
        /// </summary>
        Cancelled
    }
}
