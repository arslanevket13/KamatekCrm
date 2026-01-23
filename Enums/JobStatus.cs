namespace KamatekCrm.Enums
{
    /// <summary>
    /// İş durumlarını tanımlar
    /// </summary>
    public enum JobStatus
    {
        /// <summary>
        /// Beklemede
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Devam Ediyor
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Parça Bekleniyor
        /// </summary>
        WaitingForParts = 2,

        /// <summary>
        /// Onay Bekleniyor
        /// </summary>
        WaitingForApproval = 3,

        /// <summary>
        /// Tamamlandı
        /// </summary>
        Completed = 4,

        /// <summary>
        /// İptal Edildi
        /// </summary>
        Cancelled = 5
    }
}
