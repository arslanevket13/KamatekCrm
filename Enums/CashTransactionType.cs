namespace KamatekCrm.Enums
{
    /// <summary>
    /// Kasa işlem türü
    /// </summary>
    public enum CashTransactionType
    {
        /// <summary>
        /// Nakit Gelir (Müşteri ödemesi, perakende satış)
        /// </summary>
        CashIncome = 0,

        /// <summary>
        /// Kredi Kartı Gelir
        /// </summary>
        CardIncome = 1,

        /// <summary>
        /// Gider / Masraf (Fatura, kira, malzeme alımı vb.)
        /// </summary>
        Expense = 2,

        /// <summary>
        /// Havale/EFT Gelir
        /// </summary>
        TransferIncome = 3,

        /// <summary>
        /// Havale/EFT Gider
        /// </summary>
        TransferExpense = 4
    }
}
