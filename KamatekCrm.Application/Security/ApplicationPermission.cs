namespace KamatekCrm.ApplicationCore.Security;

/// <summary>
/// UI görünürlüğünden bağımsız olarak use-case ve servis girişlerinde denetlenen izinler.
/// </summary>
public enum ApplicationPermission
{
    ManageServiceJobs,
    ExecuteSales,
    AdjustInventory,
    ApprovePurchase,
    ViewFinance,
    DeleteRecords,
    AccessSettings,
    ManageUsers,
    ViewCustomerContactData,
    ViewCustomerIdentityData
}
