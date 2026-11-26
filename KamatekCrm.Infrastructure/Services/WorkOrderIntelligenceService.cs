using System.Globalization;
using System.Text;
using System.Text.Json;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.Infrastructure.Services;

public sealed class WorkOrderIntelligenceService : IWorkOrderIntelligenceService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<WorkOrderIntelligenceService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WorkOrderIntelligenceService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<WorkOrderIntelligenceService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<Result<WorkOrderCustomerMemoryDto>> GetCustomerMemoryAsync(
        int customerId,
        IEnumerable<string> categories,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var normalizedCategories = categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(NormalizeCategoryToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var ninetyDaysAgo = now.AddDays(-90);

            var pastJobsQuery = context.ServiceJobs
                .AsNoTracking()
                .Where(j => j.CustomerId == customerId && !j.IsDeleted && j.Status != JobStatus.Cancelled);

            int pastJobCount = await pastJobsQuery.CountAsync(cancellationToken);

            var recentJobs = await pastJobsQuery
                .OrderByDescending(j => j.CreatedDate)
                .Take(10)
                .Select(j => new
                {
                    j.Id,
                    j.Title,
                    j.JobCategory,
                    j.CategoriesJson,
                    j.Status,
                    j.CreatedDate,
                    j.CompletedDate,
                    j.TotalAmount
                })
                .ToListAsync(cancellationToken);

            var lastThree = recentJobs
                .Take(3)
                .Select(j => new CustomerPastJobSummaryDto(
                    j.Id,
                    string.IsNullOrWhiteSpace(j.Title) ? $"İş Emri #{j.Id}" : j.Title,
                    FormatCategoriesDisplay(j.CategoriesJson, j.JobCategory),
                    j.Status,
                    j.CreatedDate,
                    j.CompletedDate,
                    FinancialCalculator.RoundMoney(j.TotalAmount)))
                .ToList();

            decimal debtSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerId && t.Type == TransactionType.Debt)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

            decimal creditSum = await context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerId &&
                            (t.Type == TransactionType.Payment ||
                             t.Type == TransactionType.CreditNote ||
                             t.Type == TransactionType.Refund))
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

            decimal openBalance = FinancialCalculator.RoundMoney(Math.Max(0m, debtSum - creditSum));

            bool hasRepeatWarning = false;
            string? repeatMessage = null;
            int? repeatJobId = null;

            if (normalizedCategories.Count > 0)
            {
                foreach (var job in recentJobs)
                {
                    var effectiveDate = job.CompletedDate ?? job.CreatedDate;
                    if (effectiveDate < ninetyDaysAgo) continue;

                    var jobTokens = ExtractCategoryTokens(job.CategoriesJson, job.JobCategory);
                    var matched = jobTokens.FirstOrDefault(t => normalizedCategories.Contains(t));
                    if (!string.IsNullOrEmpty(matched))
                    {
                        hasRepeatWarning = true;
                        repeatJobId = job.Id;
                        string categoryLabel = FormatCategoryLabel(matched);
                        repeatMessage =
                            $"Son 90 gün içinde '{categoryLabel}' kategorisinde servis/montaj yapılmış (#{job.Id} — {effectiveDate.ToLocalTime():dd.MM.yyyy}). Garanti kapsamını kontrol edin.";
                        break;
                    }
                }
            }

            return Result.Success(new WorkOrderCustomerMemoryDto(
                customerId,
                pastJobCount,
                lastThree,
                openBalance,
                hasRepeatWarning,
                repeatMessage,
                repeatJobId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Müşteri hafızası sorgulanamadı (CustomerId: {CustomerId})", customerId);
            return Result.Failure<WorkOrderCustomerMemoryDto>("Müşteri geçmişi yüklenirken bir hata oluştu.");
        }
    }

    public IReadOnlyList<WorkOrderAlertBadgeDto> EvaluateJobAlerts(ServiceJob job, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        bool isTerminal = job.Status is JobStatus.Cancelled or JobStatus.Rejected;
        bool isClosed = job.Status is JobStatus.Completed or JobStatus.Delivered or JobStatus.Cancelled or JobStatus.Rejected;

        var alerts = new List<WorkOrderAlertBadgeDto>();
        if (isTerminal) return alerts;

        if (!isClosed && !job.AssignedUserId.HasValue && !job.AssignedTechnicianId.HasValue && string.IsNullOrWhiteSpace(job.AssignedTechnician))
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.UnassignedTech,
                "Teknisyen atanmadı",
                WorkOrderSeverity.Warning));
        }

        if (job.Status == JobStatus.DiscoveryCompleted && !job.IsConvertedToQuote)
        {
            var refTime = job.ModifiedDate ?? job.CreatedDate;
            if ((now - refTime).TotalHours >= 48)
            {
                int days = Math.Max(2, (int)Math.Floor((now - refTime).TotalDays));
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.DiscoveryDoneNoQuote48h,
                    $"{days} gündür teklif bekliyor",
                    WorkOrderSeverity.Warning));
            }
        }

        if (job.Status is JobStatus.ConvertedToQuote or JobStatus.Quoting or JobStatus.WaitingForApproval)
        {
            var sentRef = job.ProposalSentDate ?? job.ModifiedDate ?? job.CreatedDate;
            if ((now - sentRef).TotalHours >= 72)
            {
                int days = Math.Max(3, (int)Math.Floor((now - sentRef).TotalDays));
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.QuoteSentNoReply72h,
                    $"{days} gündür yanıt yok",
                    WorkOrderSeverity.Warning));
            }
        }

        if (job.Status is JobStatus.InstallationCompleted or JobStatus.Delivered && job.TotalAmount > 0 && !job.IsCustomerApproved)
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.InstalledUnpaid,
                "Tahsilat bekliyor",
                WorkOrderSeverity.Warning));
        }

        if (!isClosed && job.SlaDeadline.HasValue)
        {
            if (job.SlaDeadline.Value < now)
            {
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.SlaAtRisk,
                    "SLA aşıldı",
                    WorkOrderSeverity.Critical));
            }
            else if ((job.SlaDeadline.Value - now).TotalHours <= 24)
            {
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.SlaAtRisk,
                    "SLA riskli (<24s)",
                    WorkOrderSeverity.Warning));
            }
        }

        if (!isClosed && job.NextFollowUpUtc.HasValue && job.NextFollowUpUtc.Value <= now)
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.FollowUpDue,
                "Takip zamanı geldi",
                WorkOrderSeverity.Critical));
        }

        return alerts;
    }

    public IReadOnlyList<WorkOrderAlertBadgeDto> EvaluateJobAlerts(ServiceJobRowDto row, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        bool isTerminal = row.Status is JobStatus.Cancelled or JobStatus.Rejected;
        bool isClosed = row.Status is JobStatus.Completed or JobStatus.Delivered or JobStatus.Cancelled or JobStatus.Rejected;

        var alerts = new List<WorkOrderAlertBadgeDto>();
        if (isTerminal) return alerts;

        if (!isClosed && !row.AssignedTechnicianId.HasValue && string.IsNullOrWhiteSpace(row.AssignedTechnician))
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.UnassignedTech,
                "Teknisyen atanmadı",
                WorkOrderSeverity.Warning));
        }

        if (row.Status == JobStatus.DiscoveryCompleted && !row.IsConvertedToQuote && !row.QuotationId.HasValue)
        {
            var refTime = row.ModifiedDate ?? row.CreatedDate;
            if ((now - refTime).TotalHours >= 48)
            {
                int days = Math.Max(2, (int)Math.Floor((now - refTime).TotalDays));
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.DiscoveryDoneNoQuote48h,
                    $"{days} gündür teklif bekliyor",
                    WorkOrderSeverity.Warning));
            }
        }

        if (row.Status is JobStatus.ConvertedToQuote or JobStatus.Quoting or JobStatus.WaitingForApproval)
        {
            var sentRef = row.ProposalSentDate ?? row.ModifiedDate ?? row.CreatedDate;
            if ((now - sentRef).TotalHours >= 72)
            {
                int days = Math.Max(3, (int)Math.Floor((now - sentRef).TotalDays));
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.QuoteSentNoReply72h,
                    $"{days} gündür yanıt yok",
                    WorkOrderSeverity.Warning));
            }
        }

        if (row.Status is JobStatus.InstallationCompleted or JobStatus.Delivered && !row.IsPaid && row.TotalAmount > 0)
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.InstalledUnpaid,
                "Tahsilat bekliyor",
                WorkOrderSeverity.Warning));
        }

        if (!isClosed && row.SlaDeadline.HasValue)
        {
            if (row.SlaDeadline.Value < now)
            {
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.SlaAtRisk,
                    "SLA aşıldı",
                    WorkOrderSeverity.Critical));
            }
            else if ((row.SlaDeadline.Value - now).TotalHours <= 24)
            {
                alerts.Add(new WorkOrderAlertBadgeDto(
                    WorkOrderAlertCode.SlaAtRisk,
                    "SLA riskli (<24s)",
                    WorkOrderSeverity.Warning));
            }
        }

        if (!isClosed && row.NextFollowUpUtc.HasValue && row.NextFollowUpUtc.Value <= now)
        {
            alerts.Add(new WorkOrderAlertBadgeDto(
                WorkOrderAlertCode.FollowUpDue,
                "Takip zamanı geldi",
                WorkOrderSeverity.Critical));
        }

        return alerts;
    }

    public async Task<Result<WorkOrderProfitabilityDto>> CalculateProfitabilityAsync(
        int serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var job = await context.ServiceJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == serviceJobId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<WorkOrderProfitabilityDto>($"İş emri bulunamadı (ID: {serviceJobId}).");
            }

            var latestQuote = await context.WorkOrderQuotations
                .AsNoTracking()
                .Include(q => q.Items)
                .Where(q => q.ServiceJobId == serviceJobId)
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var installation = await context.InstallationOrders
                .AsNoTracking()
                .Include(i => i.Materials)
                .FirstOrDefaultAsync(i => i.ServiceJobId == serviceJobId, cancellationToken);

            var jobItems = await context.ServiceJobItems
                .AsNoTracking()
                .Where(i => i.ServiceJobId == serviceJobId)
                .ToListAsync(cancellationToken);

            var delivery = await context.JobDeliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.ServiceJobId == serviceJobId, cancellationToken);

            // Maliyet hesabı önceliği: Montaj Malzemeleri > Teklif Kalemleri > İş Emri Kalemleri
            var costLines = new List<(int? ProductId, decimal Quantity, decimal FallbackUnitCost)>();
            if (installation is not null && installation.Materials.Count > 0)
            {
                foreach (var m in installation.Materials)
                {
                    costLines.Add((m.ProductId, m.Quantity, 0m));
                }
            }
            else if (latestQuote is not null && latestQuote.Items.Count > 0)
            {
                foreach (var q in latestQuote.Items)
                {
                    costLines.Add((q.ProductId, q.Quantity, 0m));
                }
            }
            else
            {
                foreach (var ji in jobItems)
                {
                    costLines.Add((ji.ProductId, ji.QuantityUsed, ji.UnitCost));
                }
            }

            var productIds = costLines
                .Where(l => l.ProductId.HasValue)
                .Select(l => l.ProductId!.Value)
                .Distinct()
                .ToList();

            var productCosts = productIds.Count == 0
                ? new Dictionary<int, decimal>()
                : await context.Products
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, Cost = p.AverageCost > 0 ? p.AverageCost : p.PurchasePrice })
                    .ToDictionaryAsync(p => p.Id, p => p.Cost, cancellationToken);

            decimal materialCost = 0m;
            foreach (var line in costLines)
            {
                decimal unitCost = line.ProductId.HasValue && productCosts.TryGetValue(line.ProductId.Value, out var cost)
                    ? cost
                    : line.FallbackUnitCost;
                materialCost += unitCost * line.Quantity;
            }

            materialCost = FinancialCalculator.RoundMoney(materialCost);
            decimal quotedTotal = FinancialCalculator.RoundMoney(latestQuote?.TotalAmount ?? job.TotalAmount);
            decimal laborFee = FinancialCalculator.RoundMoney(latestQuote?.LaborCost ?? job.LaborCost);
            decimal discount = FinancialCalculator.RoundMoney(latestQuote?.DiscountAmount ?? job.DiscountAmount);
            decimal estimatedProfit = FinancialCalculator.RoundMoney(quotedTotal - materialCost);
            decimal marginPercent = quotedTotal > 0m
                ? FinancialCalculator.RoundMoney((estimatedProfit / quotedTotal) * 100m)
                : 0m;

            decimal collectedAmount = FinancialCalculator.RoundMoney(delivery?.PaidAmount ?? 0m);
            decimal remainingBalance = FinancialCalculator.RoundMoney(Math.Max(0m, quotedTotal - collectedAmount));

            return Result.Success(new WorkOrderProfitabilityDto(
                serviceJobId,
                materialCost,
                quotedTotal,
                laborFee,
                discount,
                estimatedProfit,
                marginPercent,
                collectedAmount,
                remainingBalance));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İş emri kârlılık hesabı başarısız (ServiceJobId: {ServiceJobId})", serviceJobId);
            return Result.Failure<WorkOrderProfitabilityDto>("Kârlılık hesaplanırken bir hata oluştu.");
        }
    }

    public async Task<Result<IReadOnlyDictionary<int, int>>> GetStockAvailabilityMapAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ids = productIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return Result.Success<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var invMap = await context.Inventories
                .AsNoTracking()
                .Where(i => i.ProductId.HasValue && ids.Contains(i.ProductId.Value))
                .GroupBy(i => i.ProductId!.Value)
                .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Total, cancellationToken);

            var reservedMap = await context.StockReservations
                .AsNoTracking()
                .Where(r => ids.Contains(r.ProductId) && r.IsActive)
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Reserved = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Reserved, cancellationToken);

            var result = new Dictionary<int, int>(ids.Count);
            foreach (var id in ids)
            {
                int total = invMap.TryGetValue(id, out var t) ? t : 0;
                int reserved = reservedMap.TryGetValue(id, out var r) ? r : 0;
                result[id] = Math.Max(0, total - reserved);
            }

            return Result.Success<IReadOnlyDictionary<int, int>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stok uygunluk haritası okunamadı.");
            return Result.Failure<IReadOnlyDictionary<int, int>>("Stok uygunluk bilgisi alınamadı.");
        }
    }

    public IReadOnlyList<DiscoveryChecklistItem> GetCategoryDiscoveryTemplates(string category)
    {
        string token = NormalizeCategoryToken(category);
        string[] titles = token switch
        {
            "cctv" or "kamera" or "security" =>
            [
                "Kamera montaj noktaları ve görüş açıları belirlendi",
                "Kayıt cihazı (NVR/DVR) ve enerji/UPS konumu tespit edildi",
                "Kablo güzergahı, metraj ve borulama ihtiyacı ölçüldü",
                "İnternet / mobil izleme altyapısı kontrol edildi",
                "Gece görüş ve aydınlatma koşulları değerlendirildi"
            ],
            "videointercom" or "diafon" =>
            [
                "Daire sayısı, blok girişi ve zil paneli yeri doğrulandı",
                "Mevcut tesisat kablo tipi (CAT6 / DT8) kontrol edildi",
                "Daire içi monitör montaj noktaları incelendi",
                "Kapı otomatiği ve güç kaynağı uyumu kontrol edildi"
            ],
            "firealarm" or "yangin" =>
            [
                "Mahal planı ve dedektör yerleşim bölgeleri belirlendi",
                "Yangın santrali yeri ve 220V/akü beslemesi kontrol edildi",
                "Siren, flaşör ve ihbar butonu noktaları işaretlendi",
                "Yangına dayanıklı kablo (J-H(St)H) metrajı hesaplandı"
            ],
            "burglaralarm" or "alarm" =>
            [
                "Giriş kapısı manyetik kontak ve PIR dedektör noktaları belirlendi",
                "Alarm paneli, tuş takımı ve dış siren konumu tespit edildi",
                "GSM / GPRS / IP haberleşme çekim kalitesi test edildi",
                "Evcil hayvan veya çevresel yanlış alarm riskleri not edildi"
            ],
            "smarthome" or "akilliev" or "automation" =>
            [
                "Aydınlatma, panjur ve iklimlendirme kontrol hatları listelendi",
                "Elektrik panosu modül boşluğu ve nötr hattı kontrol edildi",
                "Wi-Fi / Zigbee / KNX haberleşme kapsama alanı ölçüldü",
                "Kullanıcı senaryoları ve mobil kontrol beklentileri alındı"
            ],
            "fiberoptic" or "fiber" or "network" =>
            [
                "Rack kabinet konumları ve patch panel port ihtiyacı belirlendi",
                "Fiber kablo güzergahı (iç/dış ortam) ve metraj ölçüldü",
                "Füzyon ek / sonlandırma kutusu ve pig-tail sayısı hesaplandı",
                "Aktif cihaz (Switch/SFP) uyumluluğu doğrulandı"
            ],
            _ =>
            [
                "Mevcut saha ve altyapı durumu incelendi",
                "Müşteri beklentisi ve teknik gereksinimler netleştirildi",
                "Gerekli malzeme, kablo metrajı ve işçilik süresi tahminlendi",
                "Saha fotoğrafları ve ölçüm notları kaydedildi"
            ]
        };

        return titles.Select(t => new DiscoveryChecklistItem { Title = t }).ToList();
    }

    public async Task<Result<IReadOnlyList<QuickMaterialKitDto>>> GetQuickMaterialKitsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var products = await context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.Id, p.ProductName, p.SalePrice })
                .ToListAsync(cancellationToken);

            var stockResult = await GetStockAvailabilityMapAsync(products.Select(p => p.Id), cancellationToken);
            IReadOnlyDictionary<int, int> stockMap = stockResult.IsSuccess && stockResult.Value is not null
                ? stockResult.Value
                : new Dictionary<int, int>();

            QuickMaterialKitItemDto MatchItem(string displayName, int qty, params string[] keywords)
            {
                var found = products.FirstOrDefault(p =>
                    keywords.Any(k => p.ProductName.Contains(k, StringComparison.OrdinalIgnoreCase)));

                if (found is null)
                {
                    return new QuickMaterialKitItemDto(null, displayName, qty, 0m, 0, false);
                }

                int avail = stockMap.TryGetValue(found.Id, out var s) ? s : 0;
                return new QuickMaterialKitItemDto(
                    found.Id,
                    found.ProductName,
                    qty,
                    FinancialCalculator.RoundMoney(found.SalePrice),
                    avail,
                    avail >= qty);
            }

            var kits = new List<QuickMaterialKitDto>
            {
                new(
                    "cctv-4ch",
                    "4 Kameralı IP Güvenlik Seti",
                    "Kamera",
                    "4 IP Kamera, 4 Kanal NVR, 1TB Disk, PoE Switch ve CAT6 kablo",
                    new[]
                    {
                        MatchItem("IP Güvenlik Kamerası", 4, "kamera", "ipc", "bullet", "dome"),
                        MatchItem("4 Kanal NVR Kayıt Cihazı", 1, "nvr", "kayıt"),
                        MatchItem("Güvenlik Harddisk (HDD)", 1, "hdd", "disk", "skyhawk", "purple"),
                        MatchItem("PoE Switch", 1, "poe", "switch"),
                        MatchItem("CAT6 Kablo (Metre)", 100, "cat6", "kablo")
                    }),
                new(
                    "intercom-std",
                    "Apartman Görüntülü Diafon Seti",
                    "Diafon",
                    "Zil Paneli, Daire Monitörleri, Güç Kaynağı ve Bus/CAT6 Kablo",
                    new[]
                    {
                        MatchItem("Diafon Zil Paneli", 1, "zil", "panel", "diafon"),
                        MatchItem("Görüntülü Diafon Monitörü", 4, "monitör", "diafon", "interkom"),
                        MatchItem("Diafon Güç Kaynağı", 1, "güç kaynağı", "adaptör", "trafo"),
                        MatchItem("Diafon Kablosu (Metre)", 100, "dt8", "cat6", "kablo")
                    }),
                new(
                    "fire-starter",
                    "Yangın İhbar Başlangıç Paketi",
                    "Yangın",
                    "Yangın Santrali, Optik Duman Dedektörü, İhbar Butonu ve Siren",
                    new[]
                    {
                        MatchItem("Yangın İhbar Santrali", 1, "yangın santral", "santral"),
                        MatchItem("Optik Duman Dedektörü", 6, "duman", "dedektör"),
                        MatchItem("Yangın İhbar Butonu", 2, "buton", "ihbar"),
                        MatchItem("Flaşörlü Yangın Sireni", 2, "siren")
                    }),
                new(
                    "alarm-home",
                    "Hırsız Alarm Temel Kiti",
                    "Alarm",
                    "Alarm Paneli, PIR Dedektör, Manyetik Kontak ve Harici Siren",
                    new[]
                    {
                        MatchItem("Alarm Kontrol Paneli", 1, "alarm panel", "alarm"),
                        MatchItem("PIR Hareket Dedektörü", 3, "pir", "hareket"),
                        MatchItem("Manyetik Kapı Kontağı", 2, "kontak", "manyetik"),
                        MatchItem("Harici Alarm Sireni", 1, "siren")
                    })
            };

            return Result.Success<IReadOnlyList<QuickMaterialKitDto>>(kits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hazır malzeme paketleri yüklenemedi.");
            return Result.Failure<IReadOnlyList<QuickMaterialKitDto>>("Hazır malzeme paketleri alınamadı.");
        }
    }

    public string BuildWhatsAppSummaryText(ServiceJob job)
    {
        var sb = new StringBuilder();
        string customerName = job.Customer?.FullName ?? "Değerli Müşterimiz";
        sb.AppendLine($"Merhaba {customerName},");
        sb.AppendLine($"Kamatek CRM İş Emri #{job.Id} bilgilendirmesidir.");
        sb.AppendLine($"Durum: {job.StatusDisplay}");

        if (!string.IsNullOrWhiteSpace(job.Title))
        {
            sb.AppendLine($"Konu: {job.Title}");
        }

        if (job.ScheduledDate.HasValue)
        {
            sb.AppendLine($"Planlanan Tarih: {job.ScheduledDate.Value.ToLocalTime():dd.MM.yyyy HH:mm}");
        }

        if (!string.IsNullOrWhiteSpace(job.AssignedTechnician))
        {
            sb.AppendLine($"Sorumlu Teknisyen: {job.AssignedTechnician}");
        }

        if (job.TotalAmount > 0)
        {
            sb.AppendLine($"Toplam Tutar: {FinancialCalculator.RoundMoney(job.TotalAmount).ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} ₺");
        }

        sb.Append("Bilginize sunar, iyi günler dileriz.");
        return sb.ToString();
    }

    public string BuildWhatsAppUrl(string? phoneNumber, string message)
    {
        string digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = "90" + digits[1..];
        }
        else if (digits.Length == 10 && digits.StartsWith("5", StringComparison.Ordinal))
        {
            digits = "90" + digits;
        }

        string encoded = Uri.EscapeDataString(message ?? string.Empty);
        return string.IsNullOrWhiteSpace(digits)
            ? $"https://wa.me/?text={encoded}"
            : $"https://wa.me/{digits}?text={encoded}";
    }

    private static IReadOnlyList<string> ExtractCategoryTokens(string? categoriesJson, JobCategory fallback)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(categoriesJson) && categoriesJson != "[]")
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(categoriesJson, JsonOptions);
                if (list is not null)
                {
                    foreach (var item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            tokens.Add(NormalizeCategoryToken(item));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Geçersiz JSON varsa fallback kategoriye dönülür
            }
        }

        if (tokens.Count == 0)
        {
            tokens.Add(NormalizeCategoryToken(fallback.ToString()));
        }

        return tokens.ToList();
    }

    private static string FormatCategoriesDisplay(string? categoriesJson, JobCategory fallback)
    {
        var tokens = ExtractCategoryTokens(categoriesJson, fallback);
        return string.Join(", ", tokens.Select(FormatCategoryLabel));
    }

    private static string NormalizeCategoryToken(string raw)
    {
        string s = raw.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("İ", "i")
            .Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o")
            .Replace("ç", "c").Replace(" ", "");

        if (s.Contains("kamera") || s.Contains("cctv")) return "cctv";
        if (s.Contains("diafon") || s.Contains("intercom") || s.Contains("interkom")) return "videointercom";
        if (s.Contains("yangin") || s.Contains("fire")) return "firealarm";
        if (s.Contains("alarm") || s.Contains("burglar") || s.Contains("hirsiz")) return "burglaralarm";
        if (s.Contains("akilli") || s.Contains("smart") || s.Contains("otomasyon")) return "smarthome";
        if (s.Contains("fiber") || s.Contains("network") || s.Contains("ag")) return "fiberoptic";
        if (s.Contains("gecis") || s.Contains("access")) return "accesscontrol";
        if (s.Contains("uydu") || s.Contains("satellite")) return "satellitesystem";
        return s;
    }

    private static string FormatCategoryLabel(string token) => token switch
    {
        "cctv" => "Kamera",
        "videointercom" => "Diafon",
        "firealarm" => "Yangın",
        "burglaralarm" => "Alarm",
        "smarthome" => "Akıllı Ev",
        "fiberoptic" => "Fiber / Ağ",
        "accesscontrol" => "Geçiş Kontrol",
        "satellitesystem" => "Uydu",
        _ => token
    };
}
