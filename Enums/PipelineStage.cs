namespace KamatekCrm.Enums
{
    /// <summary>
    /// Satış Boru Hattı Aşamaları (Kanban)
    /// </summary>
    public enum PipelineStage
    {
        /// <summary>
        /// Yeni Fırsat / Potansiyel Müşteri
        /// </summary>
        Lead = 0,

        /// <summary>
        /// Teklif Verildi
        /// </summary>
        Quoted = 1,

        /// <summary>
        /// Pazarlık / Görüşme Aşamasında
        /// </summary>
        Negotiating = 2,

        /// <summary>
        /// Satış Kazanıldı (→ İş Emri Oluşturulur)
        /// </summary>
        Won = 3,

        /// <summary>
        /// Satış Kaybedildi
        /// </summary>
        Lost = 4
    }
}
