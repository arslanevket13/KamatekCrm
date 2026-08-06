using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Services;

/// <summary>
/// İş Emri Çalışma Alanı iş akışı kuralları — tek kaynak. Kurallar yalnızca
/// <see cref="WorkOrderWorkspaceInput"/> üzerinden çalışır; veritabanı veya UI bağımlılığı yoktur.
/// Bir butonun görünmesi, aktif olması ve servis tarafından kabul edilmesi aynı kurallara dayanır:
/// örn. teklif oluşturma ön koşulları (keşif tamamlandı + teknisyen + teknik tespit + çözüm +
/// en az bir malzeme) burada ve <c>ServiceJobCommandService.ConvertToQuoteAsync</c>'te iki kez doğrulanır.
/// </summary>
public sealed class WorkOrderNextActionResolver : IWorkOrderNextActionResolver
{
    /// <summary>Teklif oluşturma ön koşulları (§7). Eksik maddeler kontrol listesi olarak döner.</summary>
    private static IReadOnlyList<string> MissingQuotePreconditions(WorkOrderWorkspaceInput input)
    {
        var missing = new List<string>();
        if (!input.HasDiscoveryReport)
        {
            missing.Add("Keşif kaydı girilmedi");
        }
        if (string.IsNullOrWhiteSpace(input.DiscoveryTechnicianName))
        {
            missing.Add("Keşif teknisyeni belirtilmedi");
        }
        if (string.IsNullOrWhiteSpace(input.DiscoveryTechnicalNotes))
        {
            missing.Add("Teknik tespit girilmedi");
        }
        if (string.IsNullOrWhiteSpace(input.DiscoveryRecommendedSolution))
        {
            missing.Add("Önerilen çözüm girilmedi");
        }
        if (input.DiscoveryMaterialCount <= 0)
        {
            missing.Add("En az bir tahmini malzeme/hizmet kalemi girilmeli");
        }
        return missing;
    }

    private static bool DiscoveryIsComplete(WorkOrderWorkspaceInput input) =>
        MissingQuotePreconditions(input).Count == 0;

    public WorkOrderStage ResolveStage(WorkOrderWorkspaceInput input)
    {
        if (input.JobStatus == JobStatus.Cancelled)
        {
            return WorkOrderStage.Cancelled;
        }
        if (input.JobStatus is JobStatus.Completed or JobStatus.Delivered)
        {
            return WorkOrderStage.Closed;
        }
        if (input.JobStatus == JobStatus.InstallationCompleted)
        {
            return WorkOrderStage.Delivery;
        }
        if (input.JobStatus == JobStatus.InstallationPlanned)
        {
            return WorkOrderStage.Installation;
        }
        if (input.JobStatus is JobStatus.ConvertedToQuote or JobStatus.Quoting or JobStatus.Rejected)
        {
            return WorkOrderStage.Quotation;
        }
        if (input.JobStatus is JobStatus.DiscoveryRequest or JobStatus.PendingDiscovery or JobStatus.DiscoveryCompleted)
        {
            return WorkOrderStage.Discovery;
        }
        return WorkOrderStage.Pending;
    }

    public WorkOrderNextActionInfo ResolveNextAction(WorkOrderWorkspaceInput input)
    {
        var stage = ResolveStage(input);
        switch (stage)
        {
            case WorkOrderStage.Cancelled:
                return NextActionInfo(null, "İş iptal edildi",
                    "İptal edilmiş iş emri ilerletilemez.", null, false,
                    "İş iptal edildi", WorkOrderSeverity.Warning, null);

            case WorkOrderStage.Closed:
                return NextActionInfo(null, "İş kapatıldı",
                    "İş teslim edildi; fatura ve servis raporu üretilebilir.", null, false,
                    string.Empty, WorkOrderSeverity.Info, null);

            case WorkOrderStage.Delivery:
                if (!input.HasDelivery)
                {
                    return NextActionInfo(WorkOrderAction.CompleteDelivery, "Teslimi tamamla",
                        "Montaj tamamlandı — teslim tarihi, teslim eden ve ödeme bilgilerini kaydet.",
                        "Teslim Et", true, string.Empty, WorkOrderSeverity.Action, input.DeliveryDate);
                }
                // Teslim kaydı zaten varsa iş "Teslim Edildi" terminal durumundadır (politika
                // Delivered'dan çıkışa izin vermez) — kapatılacak ayrı bir adım yoktur.
                return NextActionInfo(null, "İş teslim edildi",
                    "Teslim kaydı mevcut — iş kapalı durumda; fatura / servis raporu üretilebilir.",
                    null, false, string.Empty, WorkOrderSeverity.Info, input.DeliveryDate);

            case WorkOrderStage.Installation:
                return NextActionInfo(WorkOrderAction.EditInstallation, "Montajı uygula",
                    "Montaj planlandı — malzemeleri, görevleri ve işçilik saatini doldurup tamamla.",
                    "Montajı Başlat", true, string.Empty, WorkOrderSeverity.Action, input.InstallationDate);

            case WorkOrderStage.Quotation:
                if (!input.QuotationStatus.HasValue)
                {
                    var missing = MissingQuotePreconditions(input);
                    return NextActionInfo(WorkOrderAction.CreateQuotation, "Teklif oluştur",
                        missing.Count == 0
                            ? "Keşif tamamlandı — keşif verilerinden teklif oluştur."
                            : "Teklif oluşturulamaz: " + string.Join("; ", missing),
                        "Teklif Oluştur", missing.Count == 0, string.Join("; ", missing),
                        missing.Count == 0 ? WorkOrderSeverity.Action : WorkOrderSeverity.Warning, null);
                }
                return input.QuotationStatus.Value switch
                {
                    QuotationStatus.Draft => NextActionInfo(WorkOrderAction.EditQuotation, "Teklifi tamamla",
                        "Teklif taslak durumunda — kalemleri ve şartları gözden geçirip müşteriye gönder.",
                        "Teklifi Düzenle", true, string.Empty, WorkOrderSeverity.Action, null),
                    QuotationStatus.Sent => NextActionInfo(WorkOrderAction.AcceptQuotation, "Müşteri cevabını kaydet",
                        "Teklif müşteriye gönderildi — kabul veya ret cevabını işle.",
                        "Cevabı Kaydet", true, string.Empty, WorkOrderSeverity.Action, null),
                    QuotationStatus.Accepted => NextActionInfo(WorkOrderAction.PlanInstallation, "Montajı planla",
                        "Teklif kabul edildi — montaj tarihi ve teknisyen belirle.",
                        "Montaj Planla", true, string.Empty, WorkOrderSeverity.Action, null),
                    QuotationStatus.Rejected => NextActionInfo(WorkOrderAction.ReviseQuotation, "Revizyon oluştur",
                        "Teklif reddedildi — gerekçeye göre yeni revizyon hazırla.",
                        "Revizyon Oluştur", true, string.Empty, WorkOrderSeverity.Warning, null),
                    QuotationStatus.Expired => NextActionInfo(WorkOrderAction.ReviseQuotation, "Geçerliliği yenile",
                        "Teklifin süresi doldu — yeni revizyon oluştur.",
                        "Revizyon Oluştur", true, string.Empty, WorkOrderSeverity.Warning, null),
                    _ => NextActionInfo(WorkOrderAction.ReviseQuotation, "Teklifi yeniden ele al",
                        "Teklif durumunu kontrol et — gerekiyorsa revizyon oluştur.",
                        "Revizyon Oluştur", true, string.Empty, WorkOrderSeverity.Info, null)
                };

            case WorkOrderStage.Discovery:
                if (!input.HasDiscoveryReport)
                {
                    return NextActionInfo(WorkOrderAction.ScheduleDiscovery, "Keşif planla",
                        "Bu iş için keşif tarihi ve teknisyen belirleyin.",
                        "Keşif Planla", true, string.Empty, WorkOrderSeverity.Action, null);
                }
                if (!DiscoveryIsComplete(input))
                {
                    var missing = MissingQuotePreconditions(input);
                    return NextActionInfo(WorkOrderAction.CompleteDiscovery, "Keşfi tamamla",
                        "Eksikler: " + string.Join("; ", missing),
                        "Keşfi Tamamla", false, string.Join("; ", missing),
                        WorkOrderSeverity.Warning, null);
                }
                return NextActionInfo(WorkOrderAction.CreateQuotation, "Teklif oluştur",
                    "Keşif tamamlandı — keşif verilerinden teklif oluştur.",
                    "Teklif Oluştur", true, string.Empty, WorkOrderSeverity.Action, null);

            default: // Pending / Talep
                if (!input.AssignedUserId.HasValue)
                {
                    return NextActionInfo(WorkOrderAction.AssignResponsible, "Sorumlu ata",
                        "İş emri oluşturuldu — bir kullanıcıyı sorumlu olarak ata.",
                        "Sorumlu Ata", true, string.Empty, WorkOrderSeverity.Action, null);
                }
                return NextActionInfo(WorkOrderAction.ScheduleDiscovery, "Keşif planla",
                    "Sorumlu atandı — keşif randevusu ve teknisyen belirleyin.",
                    "Keşif Planla", true, string.Empty, WorkOrderSeverity.Action, null);
        }
    }

    public IReadOnlyList<WorkOrderActionInfo> ResolveAllowedActions(WorkOrderWorkspaceInput input)
    {
        var actions = new List<WorkOrderActionInfo>();
        var stage = ResolveStage(input);
        bool terminal = stage is WorkOrderStage.Cancelled or WorkOrderStage.Closed;

        // ── Her zaman sunulabilen genel işlemler ──
        actions.Add(ActionInfo(WorkOrderAction.AssignResponsible, "Sorumlu Ata",
            "İşe bir kullanıcı atar.",
            input.AssignedUserId.HasValue ? "Sorumlu atandı" : "Sorumlu Ata",
            !input.AssignedUserId.HasValue,
            input.AssignedUserId.HasValue ? "Bu işe zaten sorumlu atanmış." : string.Empty,
            WorkOrderSeverity.Info, null));

        actions.Add(ActionInfo(WorkOrderAction.EditGeneralInfo, "Genel Bilgileri Düzenle",
            "Öncelik, sorumlu, hedef tarih ve notlar düzenlenir.",
            "Genel Bilgiler", !terminal,
            terminal ? "İş kapanmış/iptal edilmiş durumda düzenlenemez." : string.Empty,
            WorkOrderSeverity.Info, null));

        actions.Add(ActionInfo(WorkOrderAction.CancelWorkOrder, "İşi İptal Et",
            "İş emrini iptal eder (sonlanmamış işlerde).",
            "İşi İptal Et", !terminal,
            terminal ? "İş zaten kapanmış/iptal edilmiş." : string.Empty,
            WorkOrderSeverity.Critical, null));

        bool invoiceEligible = input.QuotationStatus == QuotationStatus.Accepted || input.HasDelivery;
        actions.Add(ActionInfo(WorkOrderAction.GenerateInvoice, "Fatura PDF",
            "Kabul edilmiş teklif + işçilik + nakliye + KDV üzerinden fatura üretir.",
            "Fatura PDF", invoiceEligible,
            invoiceEligible ? string.Empty : "Fatura için teklifin kabul edilmiş olması gerekir.",
            WorkOrderSeverity.Info, null));

        // Servis raporu her aşamada üretilebilir; yalnızca iptal edilmiş işlerde anlamsızdır.
        bool cancelled = stage == WorkOrderStage.Cancelled;
        actions.Add(ActionInfo(WorkOrderAction.GenerateServiceReport, "Servis Raporu PDF",
            "İş künyesi, keşif, montaj ve teslim özetini birleştiren servis raporu üretir.",
            "Servis Raporu", !cancelled,
            cancelled ? "İptal edilmiş iş için servis raporu üretilemez." : string.Empty,
            WorkOrderSeverity.Info, null));

        // ── Aşama işlemleri ──
        switch (stage)
        {
            case WorkOrderStage.Pending:
                actions.Add(ActionInfo(WorkOrderAction.ScheduleDiscovery, "Keşif Planla",
                    "Keşif randevusu ve teknisyen belirler.", "Keşif Planla",
                    true, string.Empty, WorkOrderSeverity.Action, null));
                break;

            case WorkOrderStage.Discovery:
                actions.Add(ActionInfo(WorkOrderAction.ScheduleDiscovery, "Keşif Planla",
                    "Keşif randevusu ve teknisyen belirler.", "Keşif Planla",
                    true, string.Empty, WorkOrderSeverity.Action, null));
                actions.Add(ActionInfo(WorkOrderAction.EditDiscovery, "Keşif Raporunu Düzenle",
                    "Teknik tespitler, çözüm, malzemeler ve ziyaretler düzenlenir.", "Keşfi Düzenle",
                    true, string.Empty, WorkOrderSeverity.Info, null));
                var discoveryMissing = MissingQuotePreconditions(input);
                actions.Add(ActionInfo(WorkOrderAction.CompleteDiscovery, "Keşfi Tamamla",
                    "Keşif raporunu tamamlar ve işi keşif tamamlandı durumuna alır.", "Keşfi Tamamla",
                    DiscoveryIsComplete(input), string.Join("; ", discoveryMissing),
                    DiscoveryIsComplete(input) ? WorkOrderSeverity.Action : WorkOrderSeverity.Warning, null));
                actions.Add(ActionInfo(WorkOrderAction.CreateQuotation, "Teklif Oluştur",
                    "Keşif verilerinden teklif oluşturur.", "Teklif Oluştur",
                    DiscoveryIsComplete(input), string.Join("; ", discoveryMissing),
                    DiscoveryIsComplete(input) ? WorkOrderSeverity.Action : WorkOrderSeverity.Warning, null));
                break;

            case WorkOrderStage.Quotation:
                if (!input.QuotationStatus.HasValue)
                {
                    var quoteMissing = MissingQuotePreconditions(input);
                    actions.Add(ActionInfo(WorkOrderAction.CreateQuotation, "Teklif Oluştur",
                        "Keşif verilerinden teklif oluşturur.", "Teklif Oluştur",
                        quoteMissing.Count == 0, string.Join("; ", quoteMissing),
                        quoteMissing.Count == 0 ? WorkOrderSeverity.Action : WorkOrderSeverity.Warning, null));
                    break;
                }

                switch (input.QuotationStatus.Value)
                {
                    case QuotationStatus.Draft:
                        actions.Add(ActionInfo(WorkOrderAction.EditQuotation, "Teklifi Düzenle",
                            "Kalemler, fiyatlar, iskonto, KDV ve şartlar düzenlenir.", "Teklifi Düzenle",
                            true, string.Empty, WorkOrderSeverity.Action, null));
                        actions.Add(ActionInfo(WorkOrderAction.SendQuotation, "Teklifi Gönder",
                            "Teklifi müşteriye gönderilmiş olarak işaretler.", "Teklifi Gönder",
                            true, string.Empty, WorkOrderSeverity.Action, null));
                        break;
                    case QuotationStatus.Sent:
                        actions.Add(ActionInfo(WorkOrderAction.AcceptQuotation, "Teklifi Kabul Et",
                            "Müşteri kabulünü kaydeder.", "Kabul Et",
                            true, string.Empty, WorkOrderSeverity.Action, null));
                        actions.Add(ActionInfo(WorkOrderAction.RejectQuotation, "Teklifi Reddet",
                            "Müşteri reddini ve gerekçesini kaydeder.", "Reddet",
                            true, string.Empty, WorkOrderSeverity.Warning, null));
                        break;
                    case QuotationStatus.Accepted:
                        actions.Add(ActionInfo(WorkOrderAction.PlanInstallation, "Montaj Planla",
                            "Kabul edilmiş teklif için montaj tarihi ve teknisyen belirler.", "Montaj Planla",
                            !input.HasInstallation,
                            input.HasInstallation ? "Montaj zaten planlanmış." : string.Empty,
                            WorkOrderSeverity.Action, null));
                        break;
                    case QuotationStatus.Rejected:
                    case QuotationStatus.Expired:
                    case QuotationStatus.Cancelled:
                        actions.Add(ActionInfo(WorkOrderAction.ReviseQuotation, "Revizyon Oluştur",
                            "Teklifin yeni bir revizyonunu taslak olarak oluşturur.", "Revizyon Oluştur",
                            true, string.Empty, WorkOrderSeverity.Action, null));
                        break;
                }
                break;

            case WorkOrderStage.Installation:
                if (!input.HasInstallation)
                {
                    actions.Add(ActionInfo(WorkOrderAction.PlanInstallation, "Montaj Planla",
                        "Kabul edilmiş teklif için montaj tarihi ve teknisyen belirler.", "Montaj Planla",
                        input.QuotationStatus == QuotationStatus.Accepted,
                        input.QuotationStatus == QuotationStatus.Accepted
                            ? string.Empty
                            : "Montaj yalnızca kabul edilmiş teklif için planlanabilir.",
                        WorkOrderSeverity.Action, null));
                }
                else
                {
                    actions.Add(ActionInfo(WorkOrderAction.EditInstallation, "Montajı Düzenle",
                        "Malzemeler, görevler ve işçilik saati düzenlenir.", "Montajı Düzenle",
                        !input.InstallationCompleted,
                        input.InstallationCompleted ? "Montaj tamamlandı; kayıt salt okunur." : string.Empty,
                        WorkOrderSeverity.Info, null));

                    // Kapı yalnızca malzemeye bağlıdır: işçilik saati, tamamlanma formunda (fiili
                    // saat) girilir ve servis CompleteInstallationAsync'te > 0 olarak doğrular.
                    // Bu buton servisi doğrudan çağırmaz; formu açar ve form, servis çağrısından
                    // önce saat doğrulamasını yapar — böylece servis asla geçersiz veri almaz.
                    var missingInstallation = new List<string>();
                    if (input.InstallationMaterialCount <= 0) missingInstallation.Add("en az bir malzeme girilmeli");
                    bool installationReady = !input.InstallationCompleted && missingInstallation.Count == 0;
                    actions.Add(ActionInfo(WorkOrderAction.CompleteInstallation, "Montajı Tamamla",
                        "Montajı tamamlar; stok tüketimini ve teslim verilerini işler.", "Montajı Tamamla",
                        installationReady,
                        input.InstallationCompleted
                            ? "Montaj zaten tamamlanmış."
                            : string.Join("; ", missingInstallation),
                        installationReady ? WorkOrderSeverity.Action : WorkOrderSeverity.Warning, null));
                }
                break;

            case WorkOrderStage.Delivery:
                // Teslim kaydı yoksa oluştur, varsa düzenle — editör her iki modu da destekler.
                actions.Add(ActionInfo(WorkOrderAction.CompleteDelivery, "Teslim Et / Düzenle",
                    "Teslim tarihi, teslim eden, imza ve ödeme bilgileri kaydedilir.",
                    input.HasDelivery ? "Teslim Düzenle" : "Teslim Et",
                    true, string.Empty, WorkOrderSeverity.Action, input.DeliveryDate));
                break;

            case WorkOrderStage.Closed:
                // Teslim edilmiş işlerde teslim/ödeme kaydı hâlâ düzenlenebilir (edit modu).
                if (input.HasDelivery)
                {
                    actions.Add(ActionInfo(WorkOrderAction.CompleteDelivery, "Teslim Düzenle",
                        "Teslim tarihi, teslim eden, imza ve ödeme bilgileri güncellenir.", "Teslim Düzenle",
                        true, string.Empty, WorkOrderSeverity.Info, input.DeliveryDate));
                }
                break;
        }

        return actions;
    }

    public IReadOnlyList<WorkOrderWarning> ResolveWarnings(WorkOrderWorkspaceInput input)
    {
        var warnings = new List<WorkOrderWarning>();
        var stage = ResolveStage(input);

        if (stage == WorkOrderStage.Cancelled)
        {
            warnings.Add(new WorkOrderWarning("Cancelled", "İş iptal edildi; yeni ilerleme yapılamaz.", WorkOrderSeverity.Warning));
            return warnings;
        }

        if (!input.AssignedUserId.HasValue)
        {
            warnings.Add(new WorkOrderWarning("NoAssignee", "Bu işe sorumlu atanmadı.", WorkOrderSeverity.Action));
        }

        bool terminal = stage is WorkOrderStage.Closed;
        if (input.SlaDeadline.HasValue && input.SlaDeadline.Value < DateTime.UtcNow && !terminal)
        {
            warnings.Add(new WorkOrderWarning("SlaBreached", "SLA son tarihi aşıldı.", WorkOrderSeverity.Critical));
        }
        else if (!input.SlaDeadline.HasValue && !terminal)
        {
            warnings.Add(new WorkOrderWarning("SlaMissing", "SLA tanımlanmadı.", WorkOrderSeverity.Info));
        }

        if (stage == WorkOrderStage.Discovery || stage == WorkOrderStage.Quotation)
        {
            foreach (var missing in MissingQuotePreconditions(input))
            {
                warnings.Add(new WorkOrderWarning("QuotePrecondition", "Teklif oluşturulamaz: " + missing, WorkOrderSeverity.Warning));
            }
        }

        if (input.QuotationStatus == QuotationStatus.Draft)
        {
            warnings.Add(new WorkOrderWarning("QuoteDraft", "Teklif taslak durumunda; müşteriye gönderilmedi.", WorkOrderSeverity.Action));
        }
        else if (input.QuotationStatus == QuotationStatus.Expired)
        {
            warnings.Add(new WorkOrderWarning("QuoteExpired", "Teklifin süresi doldu; revizyon oluşturun.", WorkOrderSeverity.Warning));
        }
        else if (input.QuotationStatus == QuotationStatus.Rejected)
        {
            warnings.Add(new WorkOrderWarning("QuoteRejected", "Teklif reddedildi; gerekçeye göre revizyon oluşturun.", WorkOrderSeverity.Warning));
        }
        else if (input.QuotationStatus == QuotationStatus.Accepted && !input.HasInstallation)
        {
            warnings.Add(new WorkOrderWarning("InstallationPending", "Kabul edilen teklif için montaj planlanmadı.", WorkOrderSeverity.Action));
        }

        if (stage == WorkOrderStage.Delivery && !input.HasDelivery)
        {
            warnings.Add(new WorkOrderWarning("DeliveryPending", "Montaj tamamlandı; teslim kaydı bekleniyor.", WorkOrderSeverity.Action));
        }

        return warnings;
    }

    private static WorkOrderNextActionInfo NextActionInfo(
        WorkOrderAction? action, string title, string description, string? buttonText,
        bool isEnabled, string disabledReason, WorkOrderSeverity severity, DateTime? dueDate) =>
        new(action, title, description, buttonText, isEnabled, disabledReason, severity, dueDate);

    private static WorkOrderActionInfo ActionInfo(
        WorkOrderAction action, string title, string description, string buttonText,
        bool isEnabled, string disabledReason, WorkOrderSeverity severity, DateTime? dueDate) =>
        new(action, title, description, buttonText, isEnabled, disabledReason, severity, dueDate);
}
