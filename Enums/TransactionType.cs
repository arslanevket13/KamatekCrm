namespace KamatekCrm.Enums
{
    /// <summary>
    /// Finansal işlem türü
    /// </summary>
    public enum TransactionType
    {
        /// <summary>
        /// Ödeme (Müşteriden alınan para)
        /// </summary>
        Payment = 0,

        /// <summary>
        /// Borç (Müşterinin borcu)
        /// </summary>
        Debt = 1
    }
}
