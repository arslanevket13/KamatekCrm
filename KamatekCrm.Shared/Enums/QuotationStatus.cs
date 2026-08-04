namespace KamatekCrm.Shared.Enums
{
    /// <summary>
    /// İş emri teklifinin (WorkOrderQuotation) yaşam döngüsü.
    /// Montaj yalnızca <see cref="Accepted"/> durumundaki teklifler için planlanabilir.
    /// </summary>
    public enum QuotationStatus
    {
        Draft = 0,
        Sent = 1,
        Accepted = 2,
        Rejected = 3,
        Cancelled = 4,
        Expired = 5
    }
}
