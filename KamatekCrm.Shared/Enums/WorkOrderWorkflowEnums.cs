namespace KamatekCrm.Shared.Enums
{
    /// <summary>
    /// İş emri çalışma alanının ana aşaması (Talep → Keşif → Teklif → Montaj → Teslim → Kapandı).
    /// Ana aşama, alt kayıt durumlarıyla (teklif durumu, montaj durumu vb.) karıştırılmaz;
    /// aşama application katmanındaki <c>IWorkOrderNextActionResolver</c> tarafından türetilir.
    /// </summary>
    public enum WorkOrderStage
    {
        Pending = 0,
        Discovery = 1,
        Quotation = 2,
        Installation = 3,
        Delivery = 4,
        Closed = 5,
        Cancelled = 6
    }

    /// <summary>
    /// Çalışma alanında kullanıcıya sunulabilecek işlemler. Bir işlemin görünmesi, aktif
    /// olması ve servis tarafından kabul edilmesi aynı business rule'a dayanır: aktiflik
    /// application katmanındaki <c>IWorkOrderNextActionResolver</c>'dan gelen
    /// <c>WorkOrderActionInfo.IsEnabled</c> değerine bağlanır. UI kendi başına karar vermez.
    /// </summary>
    public enum WorkOrderAction
    {
        AssignResponsible,
        ScheduleDiscovery,
        EditGeneralInfo,
        EditDiscovery,
        CompleteDiscovery,
        CreateQuotation,
        EditQuotation,
        SendQuotation,
        AcceptQuotation,
        RejectQuotation,
        ReviseQuotation,
        PlanInstallation,
        EditInstallation,
        CompleteInstallation,
        CompleteDelivery,
        GenerateInvoice,
        GenerateServiceReport,
        CloseWorkOrder,
        CancelWorkOrder
    }

    /// <summary>İşlem ve uyarıların önem derecesi (UI kart rengi ve sıralaması için).</summary>
    public enum WorkOrderSeverity
    {
        Info,
        Action,
        Warning,
        Critical
    }
}
