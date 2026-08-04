namespace KamatekCrm.Shared.Enums
{
    public enum JobStatus
    {
        Pending = 0,
        InProgress = 1,
        WaitingForParts = 2,
        WaitingForApproval = 3,
        Completed = 4,
        Cancelled = 5,
        Rejected = 6,          // Müşterinin keşif sonrası teklifi reddetmesi
        PendingDiscovery = 7,   // Keşif Bekliyor
        DiscoveryCompleted = 8, // Keşif Yapıldı
        Quoting = 9,            // Teklif Aşamasında
        DiscoveryRequest = 10,  // Keşif Talebi
        ConvertedToQuote = 11,  // Teklife Dönüştürüldü
        InstallationPlanned = 12, // Montaj Yapılacak
        InstallationCompleted = 13 // Montaj Tamamlandı
    }
}
