using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.WorkOrders;
using KamatekCrm.Settings;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using KamatekCrm.Shared.Services;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.Services
{
    public class PdfService : IQuotePdfService, IServiceReportPdfService, IInvoicePdfService, IPurchaseOrderPdfService,
        IDiscoveryPdfService, IQuotationPdfService, IInstallationPdfService
    {
        private readonly IPersonalDataProtectionService? _personalDataProtection;
        private readonly IAuditTrailService _auditTrail;

        static PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public PdfService(
            IPersonalDataProtectionService personalDataProtection,
            IAuditTrailService auditTrail)
        {
            _personalDataProtection = personalDataProtection;
            _auditTrail = auditTrail;
        }

        private string Protect(string? value, PersonalDataKind kind)
        {
            if (_personalDataProtection is not null)
                return _personalDataProtection.Protect(value, kind);

            return kind == PersonalDataKind.Address ? "Adres bilgisi kısıtlı" : "••••";
        }

        private void AuditSensitiveDocumentAccess(string documentType, int? customerId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _auditTrail.WriteAsync(
                        AuditActionType.View,
                        "CustomerDocument",
                        customerId?.ToString(),
                        $"{documentType} oluşturma sırasında müşteri iletişim verilerine erişildi.").ConfigureAwait(false);
                    if (result.IsFailure)
                        System.Diagnostics.Debug.WriteLine(result.Error);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log yazımı başarısız: {ex.Message}");
                }
            });
        }

        private static class BrandColors
        {
            public static string Primary => AppSettings.GetBrandProfile().PrimaryBrandColorHex ?? "#182649";
            public static string Secondary => AppSettings.GetBrandProfile().AccentBrandColorHex ?? "#D11920";
            public static string Accent => Secondary;
            public static string TextPrimary => Primary;
            public static string TextSecondary => "#757575";
            public static string LightGray => "#F5F5F5";
            public static string TableHeader => "#EEF2F6";
            public static string Success => "#4CAF50";
            public static string Warning => "#FF9800";
            public static string Danger => "#F44336";
        }

        private static byte[]? _cachedLogoBytes;
        private static readonly object _logoLock = new();

        public byte[]? GetLogoBytes()
        {
            var brand = AppSettings.GetBrandProfile();
            return GetActiveLogoBytes(brand);
        }

        private byte[]? GetActiveLogoBytes(CompanyBrandProfile? brand = null)
        {
            brand ??= AppSettings.GetBrandProfile();

            // 1. Özel Base64 Logo (LAN ve Taşınabilirlik Önceliği)
            if (!string.IsNullOrWhiteSpace(brand.CustomLogoBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(brand.CustomLogoBase64);
                    if (bytes != null && bytes.Length > 0)
                        return bytes;
                }
                catch { }
            }

            // 2. Özel Logo Dosya Yolu
            if (!string.IsNullOrWhiteSpace(brand.CustomLogoPath) && File.Exists(brand.CustomLogoPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(brand.CustomLogoPath);
                    if (bytes != null && bytes.Length > 0)
                        return bytes;
                }
                catch { }
            }

            // 3. Fallback: Varsayılan Kamatek Kurumsal Logosu (Önbellekli)
            if (_cachedLogoBytes != null) return _cachedLogoBytes;

            lock (_logoLock)
            {
                if (_cachedLogoBytes != null) return _cachedLogoBytes;

                try
                {
                    if (System.Windows.Application.Current != null)
                    {
                        var uri = new Uri("pack://application:,,,/Assets/Images/KamatekLogo.png");
                        var streamInfo = System.Windows.Application.GetResourceStream(uri);
                        if (streamInfo != null)
                        {
                            using var ms = new MemoryStream();
                            streamInfo.Stream.CopyTo(ms);
                            _cachedLogoBytes = ms.ToArray();
                            return _cachedLogoBytes;
                        }
                    }
                }
                catch { }

                var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "KamatekLogo.png");
                var jpgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "KamatekLogo.jpg");

                if (File.Exists(pngPath)) _cachedLogoBytes = File.ReadAllBytes(pngPath);
                else if (File.Exists(jpgPath)) _cachedLogoBytes = File.ReadAllBytes(jpgPath);

                return _cachedLogoBytes;
            }
        }

        private byte[]? GetActiveStampBytes(CompanyBrandProfile? brand = null)
        {
            brand ??= AppSettings.GetBrandProfile();
            if (!brand.ShowStampInPdf || string.IsNullOrWhiteSpace(brand.StampSignatureBase64))
                return null;

            try
            {
                return Convert.FromBase64String(brand.StampSignatureBase64);
            }
            catch
            {
                return null;
            }
        }

        public void GenerateProjectQuote(ServiceProject project, List<ScopeNode> rootNodes, string filePath)
        {
            AuditSensitiveDocumentAccess("Proje teklifi", project.Customer?.Id);
            var logoBytes = GetLogoBytes();

            var flattenedItems = FlattenScopeNodesWithImages(rootNodes);
            var bom = ProjectScopeService.GenerateConsolidatedBom(rootNodes, project?.DiscountPercent ?? 0m, project?.KdvRate ?? 20m);
            var totalAmount = bom.GrandTotal;
            var totalItems = bom.TotalItemCount;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => ComposeProfessionalHeader(c, project, logoBytes, totalAmount));
                    page.Content().Element(c => ComposeProfessionalContent(c, project, flattenedItems, rootNodes, totalAmount, totalItems));
                    page.Footer().Element(c => ComposeProfessionalFooter(c));
                });
            })
            .GeneratePdf(filePath);
        }

        #region Professional Header

        private void ComposeProfessionalHeader(IContainer container, ServiceProject project, byte[]? logoBytes, decimal totalAmount)
        {
            container.Column(col =>
            {
                // Üst Banner - Beyaz / Modern Tasarım
                col.Item().Padding(20).PaddingBottom(10).Row(row =>
                {
                    // Sol: Kamera / Güvenlik Logosu
                    row.RelativeItem().Column(c =>
                    {
                        var brand = AppSettings.GetBrandProfile();
                        if (logoBytes != null && logoBytes.Length > 0)
                        {
                            c.Item().Width(240).Image(logoBytes).FitArea();
                        }
                        else
                        {
                            c.Item().Text(brand.CompanyName).FontSize(28).Bold().FontColor(BrandColors.Accent);
                            if (!string.IsNullOrWhiteSpace(brand.CompanySubtitle))
                                c.Item().Text(brand.CompanySubtitle).FontSize(10).FontColor(BrandColors.Primary);
                            if (!string.IsNullOrWhiteSpace(brand.CompanySloganOrBranch))
                                c.Item().Text(brand.CompanySloganOrBranch).FontSize(8.5f).Italic().FontColor(BrandColors.TextSecondary);
                        }
                    });

                    // Sağ: Başlık ve Tarih
                    row.ConstantItem(250).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("Teklif No: " + (project.ProjectCode ?? "TEK-" + DateTime.Now.ToString("yyyyMMdd"))).FontSize(10).FontColor(BrandColors.TextSecondary);
                        c.Item().AlignRight().Text("Tarih: " + DateTime.Now.ToString("dd MMMM yyyy")).FontSize(10).FontColor(BrandColors.TextSecondary);
                        c.Item().PaddingTop(10).AlignRight().Text("TEKNİK VE TİCARİ TEKLİF").FontSize(18).Bold().FontColor(BrandColors.Primary);
                    });
                });

                // Kırmızı Accent Çizgi
                col.Item().LineHorizontal(3).LineColor(BrandColors.Secondary);

                // İkinci Satır - Hızlı Özet (Kutu İçinde)
                col.Item().PaddingTop(15).PaddingHorizontal(20).Background("#F8F9FA").Border(1).BorderColor("#E9ECEF").Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SAYIN:").FontSize(8).FontColor(BrandColors.TextSecondary);
                        c.Item().Text(project.Customer?.FullName ?? "Değerli Müşterimiz").FontSize(12).Bold().FontColor(BrandColors.Primary);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().AlignRight().Text("PROJE:").FontSize(8).FontColor(BrandColors.TextSecondary);
                        c.Item().AlignRight().Text(project.Title ?? "Sistem Kurulum Projesi").FontSize(11).Bold().FontColor(BrandColors.Primary);
                    });
                });
            });
        }

        #endregion

        #region Professional Content

        private void ComposeProfessionalContent(IContainer container, ServiceProject project, List<PdfLineItem> items, List<ScopeNode> rootNodes, decimal totalAmount, int totalItems)
        {
            container.Padding(20).Column(col =>
            {
                col.Spacing(15);

                // 1. Müşteri ve Proje Bilgileri
                col.Item().Element(c => ComposeCustomerInfo(c, project, items));

                // 2. Şirket Profili
                col.Item().Element(c => ComposeCompanyProfile(c));

                // 3. Proje Kapsamı Görselleştirme
                col.Item().Element(c => ComposeProjectVisualization(c, items, project));

                // 4. Ödeme Planı
                col.Item().Element(c => ComposePaymentPlan(c, totalAmount));

                // 5. Mahal Ağacı Malzeme Listesi (WBS)
                col.Item().Element(c => ComposeProductTable(c, items));

                // 6. Konsolide Malzeme İcmali (BOM)
                col.Item().Element(c => ComposeConsolidatedBomTable(c, project, rootNodes));

                // 7. Finansal Özet
                col.Item().Element(c => ComposeFinancialSummary(c, items, project));

                // 8. Ticari Şartlar
                col.Item().Element(c => ComposeCommercialTerms(c));

                // 9. İmza Bloğu
                col.Item().Element(c => ComposeSignatures(c));
            });
        }

        private void ComposeCustomerInfo(IContainer container, ServiceProject project, List<PdfLineItem> items)
        {
            var totalItems = items.Count(i => !i.IsSectionHeader);
            var totalUnits = items.Where(i => !i.IsSectionHeader).Sum(i => i.Quantity);

            container.Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(15).Column(c =>
                {
                    c.Item().Text("MÜŞTERİ BİLGİLERİ").FontSize(10).Bold().FontColor(BrandColors.Primary).FontColor(BrandColors.Secondary);
                    c.Item().PaddingTop(5).Text(project.Customer?.FullName ?? "Belirtilmemiş").Bold();
                    c.Item().Text(Protect(project.Customer?.FullAddress, PersonalDataKind.Address)).FontSize(9);
                    c.Item().Text($"Tel: {Protect(project.Customer?.PhoneNumber, PersonalDataKind.Phone)}").FontSize(9);
                    if (!string.IsNullOrEmpty(project.Customer?.Email))
                        c.Item().Text($"E-posta: {Protect(project.Customer.Email, PersonalDataKind.Email)}").FontSize(9);
                });

                row.ConstantItem(15);

                row.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(15).Column(c =>
                {
                    c.Item().Text("PROJE BİLGİLERİ").FontSize(10).Bold().FontColor(BrandColors.Secondary);
                    c.Item().PaddingTop(5).Text(project.Title ?? "Proje Adı").Bold();
                    c.Item().Text($"Proje Kodu: {project.ProjectCode ?? "-"}").FontSize(9);
                    c.Item().Text($"Toplam Kalem: {totalItems}").FontSize(9);
                    c.Item().Text($"Toplam Birim: {totalUnits}").FontSize(9);
                });
            });
        }

        private void ComposeCompanyProfile(IContainer container)
        {
            var brand = AppSettings.GetBrandProfile();
            container.Background("#FFFFFF").BorderLeft(4).BorderColor(BrandColors.Secondary).BorderTop(1).BorderRight(1).BorderBottom(1).BorderColor("#E0E0E0").Padding(12).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(brand.CompanyName + (!string.IsNullOrWhiteSpace(brand.CompanySubtitle) ? " " + brand.CompanySubtitle : "")).FontSize(11).Bold().FontColor(BrandColors.Primary);
                        if (!string.IsNullOrWhiteSpace(brand.CompanySloganOrBranch))
                            col.Item().Text(brand.CompanySloganOrBranch).FontSize(8.5f).FontColor(BrandColors.Secondary).Italic();
                    });
                });
                c.Item().PaddingTop(6).Row(r => 
                {
                    r.RelativeItem().Column(col => 
                    {
                        if (!string.IsNullOrWhiteSpace(brand.Address))
                            col.Item().Text($"📍 {brand.Address}").FontSize(8.5f).FontColor(BrandColors.TextSecondary);
                        if (!string.IsNullOrWhiteSpace(brand.Phone))
                            col.Item().PaddingTop(2).Text($"📞 {brand.Phone}").FontSize(8.5f).Bold().FontColor(BrandColors.Primary);
                    });
                    
                    r.RelativeItem().AlignRight().Column(col => 
                    {
                        if (!string.IsNullOrWhiteSpace(brand.Email))
                            col.Item().AlignRight().Text($"✉️ {brand.Email}").FontSize(8.5f);
                        if (!string.IsNullOrWhiteSpace(brand.Website))
                            col.Item().AlignRight().Text($"🌐 {brand.Website}").FontSize(8.5f).FontColor(BrandColors.Primary);
                        if (!string.IsNullOrWhiteSpace(brand.TaxNumber))
                            col.Item().AlignRight().Text($"V.D.: {brand.TaxOffice} - No: {brand.TaxNumber}").FontSize(8f).FontColor(BrandColors.TextSecondary);
                    });
                });
            });
        }

        private void ComposeProjectVisualization(IContainer container, List<PdfLineItem> items, ServiceProject project)
        {
            container.Column(c =>
            {
                c.Item().Text("PROJE KAPSAMI").FontSize(12).Bold().FontColor(BrandColors.Primary);

                var categoryGroups = items
                    .Where(i => !i.IsSectionHeader)
                    .GroupBy(i => i.Category ?? "Diğer")
                    .Select(g => new { Category = g.Key, Total = g.Sum(x => x.TotalPrice), Count = g.Count() })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var totalValue = categoryGroups.Sum(x => x.Total);

                c.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(BrandColors.Primary).Padding(8).Text("Kategori").FontColor(Colors.White).Bold();
                        header.Cell().Background(BrandColors.Primary).Padding(8).AlignRight().Text("Adet").FontColor(Colors.White).Bold();
                        header.Cell().Background(BrandColors.Primary).Padding(8).AlignRight().Text("Tutar").FontColor(Colors.White).Bold();
                        header.Cell().Background(BrandColors.Primary).Padding(8).Text("").FontColor(Colors.White).Bold();
                    });

                    foreach (var group in categoryGroups)
                    {
                        var percentage = totalValue > 0 ? (double)(group.Total / totalValue * 100) : 0;
                        var barWidth = (float)(percentage / 100.0 * 100);

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Text(group.Category);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text(group.Count.ToString());
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{group.Total:N0} ₺");
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(5).Row(r =>
                        {
                            int width = (int)barWidth;
                            if (width > 0)
                            {
                                r.ConstantItem(width).Background(BrandColors.Secondary).Height(15);
                            }
                            r.RelativeItem().Text($" %{percentage:N1}").FontSize(8).FontColor(BrandColors.TextSecondary).AlignRight();
                        });
                    }
                });
            });
        }

        private void ComposePaymentPlan(IContainer container, decimal totalAmount)
        {
            container.Column(c =>
            {
                c.Item().Text("ÖDEME PLANI").FontSize(12).Bold().FontColor(BrandColors.Primary);

                var installment1 = FinancialCalculator.RoundMoney(totalAmount * 0.50m);
                var installment2 = FinancialCalculator.RoundMoney(totalAmount - installment1);

                c.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(50);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(BrandColors.LightGray).Padding(8).Text("#").Bold();
                        header.Cell().Background(BrandColors.LightGray).Padding(8).Text("Açıklama").Bold();
                        header.Cell().Background(BrandColors.LightGray).Padding(8).AlignRight().Text("Oran").Bold();
                        header.Cell().Background(BrandColors.LightGray).Padding(8).AlignRight().Text("Tutar").Bold();
                    });

                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Background("#E3F2FD").Text("1").Bold();
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Background("#E3F2FD").Text("Sipariş Onayı (Peşinat)");
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Background("#E3F2FD").Text("%50");
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Background("#E3F2FD").Text($"{installment1:N2} ₺").Bold();

                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Background("#E8F5E9").Text("2").Bold();
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Background("#E8F5E9").Text("Montaj/Teslimat");
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Background("#E8F5E9").Text("%50");
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Background("#E8F5E9").Text($"{installment2:N2} ₺").Bold();
                });
            });
        }

        private void ComposeProductTable(IContainer container, List<PdfLineItem> items)
        {
            container.Column(c =>
            {
                c.Item().Text("YAPI AĞACI & MAHAL MALZEME LİSTESİ (WBS)").FontSize(12).Bold().FontColor(BrandColors.Primary);

                c.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(25);
                        cols.ConstantColumn(50);
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(40);
                        cols.ConstantColumn(40);
                        cols.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("#").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("Fotoğraf").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("Ürün / Mahal").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Miktar").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("B.Fiyat").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Toplam").FontColor(Colors.White).FontSize(8);
                    });

                    int index = 1;
                    foreach (var item in items)
                    {
                        if (item.IsSectionHeader)
                        {
                            table.Cell().ColumnSpan(6).Background("#ECEFF1").BorderBottom(1).BorderColor("#CFD8DC").Padding(4)
                                .PaddingLeft(Math.Max(5, item.Level * 10)).Text(item.Name).FontSize(9).Bold().FontColor(BrandColors.Primary);
                            continue;
                        }

                        var bgColor = index % 2 == 0 ? "#FAFAFA" : "#FFFFFF";

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(5).AlignCenter()
                            .Text(index.ToString()).FontSize(8);

                        // Fotoğraf alanı - varsa göster
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(3).AlignCenter()
                            .Element(cell => ComposeProductImage(cell, item.ImagePath));

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(5).Column(col =>
                        {
                            col.Item().Text(item.Name).FontSize(9).Bold();
                            if (!string.IsNullOrEmpty(item.ProductCode))
                                col.Item().Text($"SKU: {item.ProductCode}").FontSize(7).FontColor(BrandColors.TextSecondary);
                        });

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(5).AlignRight()
                            .Text(item.Quantity.ToString()).FontSize(9);

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(5).AlignRight()
                            .Text($"{item.UnitPrice:N2} ₺").FontSize(8);

                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bgColor).Padding(5).AlignRight()
                            .Text($"{item.TotalPrice:N2} ₺").FontSize(10).Bold().FontColor(BrandColors.Primary);

                        index++;
                    }
                });
            });
        }

        private void ComposeConsolidatedBomTable(IContainer container, ServiceProject project, List<ScopeNode> rootNodes)
        {
            var bom = ProjectScopeService.GenerateConsolidatedBom(rootNodes, project?.DiscountPercent ?? 0m, project?.KdvRate ?? 20m);
            if (!bom.Items.Any()) return;

            container.Column(c =>
            {
                c.Item().Text("KONSOLİDE MALZEME İCMALİ (ÖZET BOM)").FontSize(12).Bold().FontColor(BrandColors.Primary);

                c.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(25);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(45);
                        cols.ConstantColumn(60);
                        cols.ConstantColumn(70);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("#").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("Malzeme / Ürün Adı").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("Kullanım Mahalli").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Toplam").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("B.Fiyat").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Tutar").FontColor(Colors.White).FontSize(8);
                    });

                    int idx = 1;
                    foreach (var item in bom.Items)
                    {
                        var bg = idx % 2 == 0 ? "#FAFAFA" : "#FFFFFF";
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).AlignCenter().Text(idx.ToString()).FontSize(8);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).Text(item.ProductName).FontSize(9).Bold();
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).Text(item.LocationsSummary).FontSize(8).FontColor(BrandColors.TextSecondary);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).AlignRight().Text($"{item.TotalQuantity} Ad.").FontSize(9).Bold();
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).AlignRight().Text($"{item.UnitPrice:N2} ₺").FontSize(8);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Background(bg).Padding(5).AlignRight().Text($"{item.GrossRevenue:N2} ₺").FontSize(9).Bold().FontColor(BrandColors.Primary);
                        idx++;
                    }
                });
            });
        }

        private void ComposeProductImage(IContainer container, string? imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(imagePath);
                    container.Width(40).Height(40).Image(bytes).FitArea();
                }
                catch
                {
                    container.Text("📷").FontSize(16).AlignCenter();
                }
            }
            else
            {
                container.Text("📦").FontSize(16).AlignCenter();
            }
        }

        private void ComposeFinancialSummary(IContainer container, List<PdfLineItem> items, ServiceProject? project = null)
        {
            var subTotal = FinancialCalculator.RoundMoney(items.Where(i => !i.IsSectionHeader).Sum(i => i.TotalPrice));
            decimal discountPercent = project?.DiscountPercent ?? 0m;
            decimal kdvRate = project?.KdvRate ?? 20m;
            decimal discountAmount = FinancialCalculator.RoundMoney(subTotal * discountPercent / 100m);
            decimal netTotal = FinancialCalculator.RoundMoney(subTotal - discountAmount);
            decimal vatTotal = FinancialCalculator.RoundMoney(netTotal * kdvRate / 100m);
            decimal grandTotal = FinancialCalculator.RoundMoney(netTotal + vatTotal);

            container.PaddingTop(10).Row(r =>
            {
                r.RelativeItem(); // Sağ tarafa yaslamak için boşluk
                r.ConstantItem(250).Background("#F8F9FA").Border(1).BorderColor("#E0E0E0").Padding(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Ara Toplam:").FontSize(11).FontColor(BrandColors.TextSecondary);
                        row.RelativeItem().AlignRight().Text($"{subTotal:N2} ₺").FontSize(11).Bold().FontColor(BrandColors.Primary);
                    });

                    if (discountAmount > 0)
                    {
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text($"İskonto (%{discountPercent:N0}):").FontSize(11).FontColor(BrandColors.Warning);
                            row.RelativeItem().AlignRight().Text($"-{discountAmount:N2} ₺").FontSize(11).FontColor(BrandColors.Warning);
                        });
                    }

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"KDV (%{kdvRate:N0}):").FontSize(11).FontColor(BrandColors.TextSecondary);
                        row.RelativeItem().AlignRight().Text($"{vatTotal:N2} ₺").FontSize(11).FontColor(BrandColors.Primary);
                    });

                    col.Item().PaddingTop(10).PaddingBottom(10).LineHorizontal(1).LineColor("#CCCCCC");

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("GENEL TOPLAM").FontSize(14).Bold().FontColor(BrandColors.Primary);
                        row.RelativeItem().AlignRight().Text($"{grandTotal:N2} ₺").FontSize(18).Bold().FontColor(BrandColors.Secondary);
                    });
                });
            });
        }

        private void ComposeCommercialTerms(IContainer container)
        {
            var brand = AppSettings.GetBrandProfile();
            container.Column(col =>
            {
                // 1. Banka Hesapları (varsa)
                if (!string.IsNullOrWhiteSpace(brand.IbanTry) || !string.IsNullOrWhiteSpace(brand.BankName))
                {
                    col.Item().PaddingBottom(10).Element(c => ComposeBankAccounts(c, brand));
                }

                col.Item().Text("TİCARİ ŞARTLAR VE GARANTİ").FontSize(11).Bold().FontColor(BrandColors.Primary);

                col.Item().PaddingTop(6).Background("#FFF8E1").Border(1).BorderColor("#FFECB3").Padding(10).Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(brand.QuoteTermsText))
                    {
                        var lines = brand.QuoteTermsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            c.Item().PaddingBottom(2).Row(row =>
                            {
                                row.ConstantItem(16).Text("✓").FontColor(BrandColors.Success).FontSize(9);
                                row.RelativeItem().Text(line).FontSize(8.5f);
                            });
                        }
                    }
                    else
                    {
                        c.Item().Row(row =>
                        {
                            row.ConstantItem(16).Text("✓").FontColor(BrandColors.Success);
                            row.RelativeItem().Text($"Teklif Geçerlilik Süresi: {brand.QuoteValidityDays} gün").FontSize(8.5f);
                        });
                        c.Item().Row(row =>
                        {
                            row.ConstantItem(16).Text("✓").FontColor(BrandColors.Success);
                            row.RelativeItem().Text("Garanti Süresi: 2 yıl (malzeme) + 1 yıl (işçilik)").FontSize(8.5f);
                        });
                        c.Item().Row(row =>
                        {
                            row.ConstantItem(16).Text("✓").FontColor(BrandColors.Success);
                            row.RelativeItem().Text("Ödeme: %50 peşin, %50 teslimde").FontSize(8.5f);
                        });
                    }
                });
            });
        }

        private void ComposeBankAccounts(IContainer container, CompanyBrandProfile brand)
        {
            container.Border(1).BorderColor("#E0E0E0").Background("#F8F9FA").Padding(10).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("🏦 ÖDEME VE BANKA BİLGİLERİ").FontSize(9.5f).Bold().FontColor(BrandColors.Primary);
                    if (!string.IsNullOrWhiteSpace(brand.AccountHolder))
                        row.RelativeItem().AlignRight().Text($"Hesap Sahibi: {brand.AccountHolder}").FontSize(8.5f).Bold();
                });

                col.Item().PaddingTop(4).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(brand.BankName))
                        row.ConstantItem(140).Text($"Banka: {brand.BankName}").FontSize(8.5f);

                    if (!string.IsNullOrWhiteSpace(brand.IbanTry))
                        row.RelativeItem().Text($"IBAN (TL): {brand.IbanTry}").FontSize(8.5f).Bold().FontColor(BrandColors.Primary);

                    if (!string.IsNullOrWhiteSpace(brand.IbanUsd))
                        row.RelativeItem().Text($"IBAN (USD): {brand.IbanUsd}").FontSize(8.5f);

                    if (!string.IsNullOrWhiteSpace(brand.IbanEur))
                        row.RelativeItem().Text($"IBAN (EUR): {brand.IbanEur}").FontSize(8.5f);
                });
            });
        }

        private void ComposeSignatures(IContainer container)
        {
            var brand = AppSettings.GetBrandProfile();
            var stampBytes = GetActiveStampBytes(brand);

            container.PaddingTop(20).Column(col =>
            {
                col.Item().Text("ONAY VE YETKİLENDİRME").FontSize(11).Bold().FontColor(BrandColors.Primary).AlignCenter();
                
                col.Item().PaddingTop(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Border(1).BorderColor("#CCCCCC").Height(75).Padding(6).Column(inner =>
                        {
                            inner.Item().Text("MÜŞTERİ İMZA VE KAŞE").FontSize(8.5f).Bold().FontColor(BrandColors.Primary).AlignCenter();
                            inner.Item().PaddingTop(20).Text("Yetkili İsim / İmza:").FontSize(7.5f).FontColor(BrandColors.TextSecondary);
                        });
                        c.Item().PaddingTop(4).Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontSize(8f);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Border(1).BorderColor("#CCCCCC").Height(75).Padding(6).Column(inner =>
                        {
                            inner.Item().Text($"{brand.CompanyName.ToUpperInvariant()} İMZA VE KAŞE").FontSize(8.5f).Bold().FontColor(BrandColors.Secondary).AlignCenter();
                            if (stampBytes != null && stampBytes.Length > 0)
                            {
                                inner.Item().Height(45).AlignCenter().Image(stampBytes).FitArea();
                            }
                            else
                            {
                                inner.Item().PaddingTop(20).Text("Firma Yetkili İmza").FontSize(7.5f).FontColor(BrandColors.TextSecondary).AlignCenter();
                            }
                        });
                        c.Item().PaddingTop(4).Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontSize(8f);
                    });
                });
            });
        }

        #endregion

        #region Professional Footer

        private void ComposeProfessionalFooter(IContainer container)
        {
            var brand = AppSettings.GetBrandProfile();
            container.Column(col =>
            {
                // İnce Accent Çizgi
                col.Item().LineHorizontal(2).LineColor(BrandColors.Secondary);

                col.Item().Background(BrandColors.Primary).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(brand.CompanyName + (!string.IsNullOrWhiteSpace(brand.CompanySubtitle) ? " " + brand.CompanySubtitle : "")).FontSize(8.5f).Bold().FontColor(Colors.White);
                        c.Item().Text($"{brand.Address} • Tel: {brand.Phone} • {brand.Website}").FontSize(7.5f).FontColor("#CFD8DC");
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"Bu doküman {brand.CompanyName} sistemi üzerinden otomatik oluşturulmuştur.").FontSize(7f).FontColor("#B0BEC5");
                        c.Item().AlignRight().Text(text =>
                        {
                            text.Span("Sayfa ").FontSize(7.5f).FontColor(Colors.White);
                            text.CurrentPageNumber().FontSize(7.5f).FontColor(Colors.White);
                            text.Span(" / ").FontSize(7.5f).FontColor(Colors.White);
                            text.TotalPages().FontSize(7.5f).FontColor(Colors.White);
                        });
                    });
                });
            });
        }

        #endregion

        #region Helper Methods

        private List<PdfLineItem> FlattenScopeNodesWithImages(List<ScopeNode> nodes, int level = 0)
        {
            var list = new List<PdfLineItem>();

            foreach (var node in nodes)
            {
                if (node.Items.Any() || node.Children.Any())
                {
                    list.Add(new PdfLineItem
                    {
                        Name = node.Name,
                        IsSectionHeader = true,
                        Level = level,
                        Category = "Proje Kapsamı"
                    });
                }

                foreach (var item in node.Items)
                {
                    list.Add(new PdfLineItem
                    {
                        Name = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice,
                        IsSectionHeader = false,
                        Level = level + 1,
                        Category = GetCategoryForNode(node),
                        ProductCode = item.ProductName,
                        ImagePath = ResolveImageAbsolutePath(item.ImagePath)
                    });
                }

                if (node.Children.Any())
                {
                    list.AddRange(FlattenScopeNodesWithImages(node.Children.ToList(), level + 1));
                }
            }

            return list;
        }

        private string GetCategoryForNode(ScopeNode node)
        {
            if (node.Type == NodeType.Block) return "Blok";
            if (node.Type == NodeType.Floor) return "Kat";
            if (node.Type == NodeType.Flat) return "Daire";
            if (node.Type == NodeType.Zone) return "Bölge";
            return "Diğer";
        }

        /// <summary>
        /// Relative DB path'ini absolute dosya yoluna çözer.
        /// Hem relative format (uploads/products/xxx.webp) hem absolute format desteklenir.
        /// </summary>
        private string? ResolveImageAbsolutePath(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;

            // Zaten absolute path ise direkt kontrol et
            if (Path.IsPathRooted(imagePath))
            {
                return File.Exists(imagePath) ? imagePath : null;
            }

            // Relative path → absolute path (uygulama kök dizinine göre)
            var absolutePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                imagePath.Replace('/', Path.DirectorySeparatorChar));

            return File.Exists(absolutePath) ? absolutePath : null;
        }

        public void GenerateServiceForm(ServiceJob job, string filePath)
        {
            AuditSensitiveDocumentAccess("Servis formu", job.Customer?.Id);
            var logoBytes = GetLogoBytes();

             Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
                    
                    page.Header().Column(col => 
                    {
                        col.Item().Padding(20).PaddingBottom(10).Row(r => 
                        {
                            // Logo 
                            r.RelativeItem().Column(c => 
                            {
                                var brand = AppSettings.GetBrandProfile();
                                if (logoBytes != null && logoBytes.Length > 0)
                                {
                                    c.Item().Width(240).Image(logoBytes).FitArea();
                                }
                                else 
                                {
                                    c.Item().Text(brand.CompanyName).FontSize(28).Bold().FontColor(BrandColors.Accent);
                                    if (!string.IsNullOrWhiteSpace(brand.CompanySubtitle))
                                        c.Item().Text(brand.CompanySubtitle).FontSize(9).FontColor(BrandColors.Primary);
                                    if (!string.IsNullOrWhiteSpace(brand.CompanySloganOrBranch))
                                        c.Item().Text(brand.CompanySloganOrBranch).FontSize(8).Italic().FontColor(BrandColors.TextSecondary);
                                }
                            });

                            r.ConstantItem(200).AlignRight().Column(c => 
                            {
                                c.Item().AlignRight().Text("TEKNİK SERVİS FORMU").FontSize(18).Bold().FontColor(BrandColors.Primary);
                                c.Item().AlignRight().Text($"Kayıt No: {job.Id}").FontSize(10).FontColor(BrandColors.TextSecondary);
                                c.Item().AlignRight().Text($"Tarih: {job.CreatedDate:dd.MM.yyyy HH:mm}").FontSize(10).FontColor(BrandColors.TextSecondary);
                            });
                        });

                        col.Item().LineHorizontal(3).LineColor(BrandColors.Secondary);
                    });
                    
                    page.Content().Padding(20).Column(col => 
                    {
                        col.Spacing(15);
                        
                        // Müşteri Bilgisi Kartı
                        col.Item().Background("#F8F9FA").Border(1).BorderColor("#E9ECEF").Padding(15).Row(r => 
                        {
                            r.RelativeItem().Column(c => {
                                c.Item().Text("MÜŞTERİ:").FontSize(8).FontColor(BrandColors.TextSecondary);
                                c.Item().Text(job.Customer?.FullName ?? "-").FontSize(14).Bold().FontColor(BrandColors.Primary);
                            });
                        });

                        col.Item().Text("İŞLEM DETAYLARI").FontSize(12).Bold().FontColor(BrandColors.Primary);
                        col.Item().Background("#FFFFFF").Border(1).BorderColor("#E0E0E0").Padding(15).Text(job.Description ?? "Açıklama yok.").FontSize(10);
                        
                        col.Item().PaddingTop(20).Element(ComposeCompanyProfile); // Firma bilgilerini alt kısma ekle
                        col.Item().Element(ComposeSignatures);
                    });

                    page.Footer().Element(ComposeProfessionalFooter);
                });
            })
            .GeneratePdf(filePath);
        }

        private class PdfLineItem
        {
            public string Name { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string? ImagePath { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal TotalPrice { get; set; }
            public bool IsSectionHeader { get; set; }
            public int Level { get; set; }
        }

        #region Standard Quote PDF

        public void GenerateStandardQuote(Quote quote, string filePath)
        {
            AuditSensitiveDocumentAccess("Standart teklif", quote.Customer?.Id);
            var logoBytes = GetLogoBytes();
            var totalItems = quote.Lines.Sum(l => l.Quantity);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => ComposeStandardQuoteHeader(c, quote, logoBytes));
                    page.Content().Element(c => ComposeStandardQuoteContent(c, quote, totalItems));
                    page.Footer().Element(c => ComposeProfessionalFooter(c));
                });
            })
            .GeneratePdf(filePath);
        }

        private void ComposeStandardQuoteHeader(IContainer container, Quote quote, byte[]? logoBytes)
        {
            container.Column(col =>
            {
                // Üst Banner - Beyaz / Modern Tasarım
                col.Item().Padding(20).PaddingBottom(10).Row(row =>
                {
                    // Sol: Logo
                    row.RelativeItem().Column(c =>
                    {
                        var brand = AppSettings.GetBrandProfile();
                        if (logoBytes != null && logoBytes.Length > 0)
                        {
                            c.Item().Width(240).Image(logoBytes).FitArea();
                        }
                        else
                        {
                            c.Item().Text(brand.CompanyName).FontSize(28).Bold().FontColor(BrandColors.Accent);
                            if (!string.IsNullOrWhiteSpace(brand.CompanySubtitle))
                                c.Item().Text(brand.CompanySubtitle).FontSize(9).FontColor(BrandColors.Primary);
                            if (!string.IsNullOrWhiteSpace(brand.CompanySloganOrBranch))
                                c.Item().Text(brand.CompanySloganOrBranch).FontSize(8).Italic().FontColor(BrandColors.TextSecondary);
                        }
                    });

                    // Sağ: Başlık ve Tarih
                    row.ConstantItem(250).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("Teklif No: " + (!string.IsNullOrWhiteSpace(quote.QuoteNumber) ? quote.QuoteNumber : "TASLAK")).FontSize(10).FontColor(BrandColors.TextSecondary);
                        c.Item().AlignRight().Text("Tarih: " + quote.Date.ToString("dd MMMM yyyy")).FontSize(10).FontColor(BrandColors.TextSecondary);
                        c.Item().AlignRight().Text("Geçerlilik: " + quote.ValidUntil.ToString("dd MMMM yyyy")).FontSize(9).FontColor(BrandColors.TextSecondary);
                        c.Item().PaddingTop(10).AlignRight().Text("FİYAT TEKLİFİ").FontSize(18).Bold().FontColor(BrandColors.Primary);
                    });
                });

                // Kırmızı Accent Çizgi
                col.Item().LineHorizontal(3).LineColor(BrandColors.Secondary);

                // İkinci Satır - Müşteri Özeti
                col.Item().PaddingTop(15).PaddingHorizontal(20).Background("#F8F9FA").Border(1).BorderColor("#E9ECEF").Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SAYIN:").FontSize(8).FontColor(BrandColors.TextSecondary);
                        c.Item().Text(quote.Customer?.FullName ?? "Değerli Müşterimiz").FontSize(12).Bold().FontColor(BrandColors.Primary);
                        if (!string.IsNullOrWhiteSpace(quote.Customer?.FullAddress))
                        {
                            c.Item().Text(Protect(quote.Customer.FullAddress, PersonalDataKind.Address)).FontSize(9).FontColor(BrandColors.TextSecondary);
                        }
                        if (!string.IsNullOrWhiteSpace(quote.Customer?.PhoneNumber))
                        {
                            c.Item().Text("Tel: " + Protect(quote.Customer.PhoneNumber, PersonalDataKind.Phone)).FontSize(9).FontColor(BrandColors.TextSecondary);
                        }
                    });
                });
            });
        }

        private void ComposeStandardQuoteContent(IContainer container, Quote quote, decimal totalItems)
        {
            var brand = AppSettings.GetBrandProfile();
            container.Padding(20).Column(col =>
            {
                col.Spacing(15);

                // 1. Şirket Profili
                col.Item().Element(c => ComposeCompanyProfile(c));

                // 2. Malzeme Listesi (Tablo)
                col.Item().Element(c => ComposeStandardQuoteTable(c, quote, brand));

                // 3. Finansal Özet
                col.Item().Element(c => ComposeStandardQuoteSummary(c, quote));

                // 4. Ticari Şartlar ve Banka Bilgileri
                col.Item().Element(c => ComposeCommercialTerms(c));

                // 5. İmza Bloğu
                col.Item().Element(c => ComposeSignatures(c));
            });
        }

        private void ComposeStandardQuoteTable(IContainer container, Quote quote, CompanyBrandProfile brand)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    if (brand.ShowUnitPricesInPdf)
                    {
                        cols.RelativeColumn(3); // Ürün Adı
                        cols.RelativeColumn(1); // Miktar
                        cols.RelativeColumn(1); // Birim Fiyat
                        cols.RelativeColumn(1); // İskonto
                        cols.RelativeColumn(1); // Tutar
                    }
                    else
                    {
                        cols.RelativeColumn(5); // Ürün / Hizmet (Paket Modu)
                        cols.RelativeColumn(2); // Miktar
                    }
                });

                table.Header(header =>
                {
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).Text("Ürün / Hizmet").Bold().FontColor(BrandColors.Primary);
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                    if (brand.ShowUnitPricesInPdf)
                    {
                        header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary);
                        header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("İskonto").Bold().FontColor(BrandColors.Primary);
                        header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Tutar").Bold().FontColor(BrandColors.Primary);
                    }
                });

                foreach (var line in quote.Lines)
                {
                    var desc = !string.IsNullOrWhiteSpace(line.ProductName) ? line.ProductName : (!string.IsNullOrWhiteSpace(line.ProductCode) ? line.ProductCode : "Hizmet / Ürün");
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Text(desc).FontSize(9);
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.Quantity} {line.Unit}").FontSize(9);
                    if (brand.ShowUnitPricesInPdf)
                    {
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.UnitPrice:N2} ₺").FontSize(9);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"%{line.DiscountPercent:N0}").FontSize(9);
                        table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.LineTotal:N2} ₺").FontSize(9);
                    }
                }
            });
        }

        private void ComposeStandardQuoteSummary(IContainer container, Quote quote)
        {
            container.Row(row =>
            {
                row.RelativeItem(); // Boşluk
                row.ConstantItem(250).Background("#F8F9FA").Border(1).BorderColor("#E0E0E0").Padding(15).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Ara Toplam:").FontSize(10);
                        r.RelativeItem().AlignRight().Text($"{quote.SubTotal:C2}").FontSize(10);
                    });
                    
                    if (quote.TotalDiscount > 0)
                    {
                        c.Item().PaddingTop(5).Row(r =>
                        {
                            r.RelativeItem().Text("İskonto Toplamı:").FontSize(10).FontColor(BrandColors.Warning);
                            r.RelativeItem().AlignRight().Text($"-{quote.TotalDiscount:C2}").FontSize(10).FontColor(BrandColors.Warning);
                        });
                    }

                    c.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("KDV Toplamı:").FontSize(10);
                        r.RelativeItem().AlignRight().Text($"{quote.TotalTax:C2}").FontSize(10);
                    });

                    c.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CCCCCC");

                    c.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Text("GENEL TOPLAM:").FontSize(12).Bold().FontColor(BrandColors.Primary);
                        r.RelativeItem().AlignRight().Text($"{quote.GrandTotal:C2}").FontSize(12).Bold().FontColor(BrandColors.Secondary);
                    });
                });
            });
        }

        #region Keşif & Servis Formu PDF Şablonları

        /// <summary>
        /// Eski genel yöntem artık varsayılan olarak Keşif PDF'i üretmez.
        /// İş emrinin aşamasına göre doğru belgeyi üreten yöntemler kullanılmalıdır:
        /// Keşif → <see cref="GenerateDiscoveryReportPdf"/>, Teklif → <see cref="GenerateWorkOrderQuotationPdf"/>,
        /// Montaj → <see cref="GenerateInstallationOrderPdf"/>, Tamamlanan → <see cref="GenerateInstallationCompletionFormPdf"/>.
        /// </summary>
        public void GenerateServiceJobPdf(ServiceJob job, string filePath)
        {
            AuditSensitiveDocumentAccess("Servis işi belgesi", job.Customer?.Id);
            if (job.WorkOrderType == WorkOrderType.Discovery ||
                job.Status == JobStatus.DiscoveryRequest ||
                job.Status == JobStatus.PendingDiscovery ||
                job.Status == JobStatus.DiscoveryCompleted)
            {
                GenerateDiscoveryReportPdf(job, filePath);
                return;
            }

            throw new NotSupportedException(
                $"Durum '{job.Status}' için aşama bazlı PDF üretimi gerekir. Lütfen Keşif/Teklif/Montaj PDF servisini kullanın.");
        }

        public void GenerateServiceJobPdf(ServiceJobDocumentDto document, string filePath)
        {
            var job = new ServiceJob
            {
                Id = document.Id,
                WorkOrderType = document.WorkOrderType,
                Description = document.Description,
                DiscoveryTechnicalNotes = document.DiscoveryTechnicalNotes,
                TechnicianNotes = document.TechnicianNotes,
                AssignedTechnician = document.AssignedTechnician,
                Priority = document.Priority,
                ScheduledDate = document.ScheduledDate,
                CustomerId = document.CustomerId,
                Customer = new Customer
                {
                    Id = document.CustomerId,
                    FullName = document.CustomerName,
                    CompanyName = document.CustomerCompanyName,
                    PhoneNumber = document.CustomerPhone,
                    City = document.CustomerAddress
                }
            };
            GenerateServiceJobPdf(job, filePath);
        }

        public void GenerateDiscoveryReportPdf(ServiceJob job, string filePath)
        {
            AuditSensitiveDocumentAccess("Keşif raporu", job.Customer?.Id);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(r =>
                                {
                                    if (logoBytes != null)
                                    {
                                        r.Item().Width(200).Image(logoBytes).FitArea();
                                    }
                                    else
                                    {
                                        r.Item().Text("KAMATEK").FontSize(28).Bold().FontColor(BrandColors.Primary);
                                        r.Item().Text("ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(9).FontColor(BrandColors.Secondary);
                                    }
                                });

                                row.ConstantItem(250).AlignRight().Column(r =>
                                {
                                    r.Item().Text("KEŞİF VE SAHA TESPİT RAPORU").FontSize(16).Bold().FontColor(BrandColors.Primary);
                                    r.Item().Text($"Takip No: #{job.Id}").FontSize(10).Bold().FontColor(BrandColors.Secondary);
                                    r.Item().Text($"Tarih: {DateTime.Now:dd MMMM yyyy}").FontSize(9).FontColor(BrandColors.TextSecondary);
                                });
                            });

                            col.Item().PaddingTop(10).LineHorizontal(2).LineColor(BrandColors.Secondary);
                        });
                    });

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            // 1. Müşteri ve Saha Konum Bilgileri
                            col.Item().Border(1).BorderColor("#E0E0E0").Background("#F8F9FA").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("MÜŞTERİ VE SAHA KONUM BİLGİLERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Column(c2 =>
                                    {
                                        c2.Item().Text($"Müşteri / Firma: {job.Customer?.FullName ?? job.Customer?.CompanyName ?? "Belirtilmedi"}").Bold();
                                        c2.Item().Text($"Telefon: {Protect(job.Customer?.PhoneNumber, PersonalDataKind.Phone)}").FontSize(9);
                                        c2.Item().Text($"Adres: {Protect(job.Customer?.FullAddress, PersonalDataKind.Address)}").FontSize(9);
                                    });
                                    r.RelativeItem().Column(c3 =>
                                    {
                                        c3.Item().Text($"Teknisyen: {job.AssignedTechnician ?? "Atanmadı"}").FontSize(9);
                                        c3.Item().Text($"Planlanan Tarih: {(job.ScheduledDate.HasValue ? job.ScheduledDate.Value.ToString("dd.MM.yyyy HH:mm") : "-")}").FontSize(9);
                                        c3.Item().Text($"Öncellik: {job.Priority}").FontSize(9);
                                    });
                                });
                            });

                            // 2. Müşteri Talebi / İhtiyaç Açıklaması
                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("MÜŞTERİ TALEBİ / İHTİYAÇ AÇIKLAMASI").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(job.Description) ? "Özel bir açıklama belirtilmedi." : job.Description).FontSize(9);
                            });

                            // 3. Teknisyenin Sahadaki Teknik Tespitleri
                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("TEKNİSYENİN SAHADAKİ TESPİTLERİ VE ALTYAPI DURUMU").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(job.DiscoveryTechnicalNotes ?? job.TechnicianNotes)
                                    ? "Saha tespit notu eklenmedi."
                                    : (job.DiscoveryTechnicalNotes ?? job.TechnicianNotes)).FontSize(9);
                            });

                            // 4. Saha Çizim / Kroki / Taslak Alanı (Grid/Dotted Box)
                            col.Item().Border(1).BorderColor("#B0BEC5").Padding(10).Column(c1 =>
                            {
                                c1.Item().Text("SAHA KROKİ / TEKNİK ÇİZİM VE EK NOTLAR ALANI").FontSize(10).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(4).Text("Bu alana sahada montaj yeri, kablolama güzergahı ve yerleşim planı çizilebilir.").FontSize(8).Italic().FontColor("#78909C");
                                c1.Item().PaddingTop(8).Height(180).Border(1).BorderColor("#CFD8DC").Background("#FAFAFA").Padding(10).Text("");
                            });

                            // 5. İmza Bloğu
                            col.Item().PaddingTop(10).Row(r =>
                            {
                                r.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
                                {
                                    c1.Item().Text("TEKNİSYEN İMZA").FontSize(9).Bold().FontColor(BrandColors.Primary);
                                    c1.Item().PaddingTop(30).Text(job.AssignedTechnician ?? "Teknisyen").FontSize(8).AlignRight();
                                });
                                r.ConstantItem(20);
                                r.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
                                {
                                    c1.Item().Text("MÜŞTERİ ONAY / İMZA").FontSize(9).Bold().FontColor(BrandColors.Primary);
                                    c1.Item().PaddingTop(30).Text(job.Customer?.FullName ?? "Müşteri").FontSize(8).AlignRight();
                                });
                            });
                        });
                    });

                    page.Footer().Element(c =>
                    {
                        c.Column(col =>
                        {
                            col.Item().LineHorizontal(1).LineColor("#E0E0E0");
                            col.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Kamatek CRM Keşif Servis Formu").FontSize(8).FontColor("#9E9E9E");
                                r.RelativeItem().AlignRight().Text(x =>
                                {
                                    x.Span("Sayfa ");
                                    x.CurrentPageNumber();
                                    x.Span(" / ");
                                    x.TotalPages();
                                });
                            });
                        });
                    });
                });
            })
            .GeneratePdf(filePath);
        }

        #endregion

        #region Invoice & Purchase Order PDF
        public void GenerateInvoice(SalesOrder order, string filePath)
        {
            var quote = new Quote
            {
                QuoteNumber = !string.IsNullOrWhiteSpace(order.OrderNumber) ? order.OrderNumber : $"INV-{order.Id}",
                Date = order.Date,
                ValidUntil = order.Date.AddDays(30),
                Customer = order.Customer,
                TermsAndConditions = order.Notes,
                SubTotal = order.SubTotal,
                TotalDiscount = order.DiscountTotal,
                TotalTax = order.TaxTotal,
                GrandTotal = order.TotalAmount
            };

            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    quote.Lines.Add(new QuoteLine
                    {
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        LineTotal = item.LineTotal
                    });
                }
            }

            GenerateStandardQuote(quote, filePath);
        }

        public void GeneratePurchaseOrder(PurchaseInvoice invoice, string filePath)
        {
            var quote = new Quote
            {
                QuoteNumber = !string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? invoice.InvoiceNumber : $"PO-{invoice.Id}",
                Date = invoice.Date,
                ValidUntil = invoice.Date.AddDays(30),
                Customer = new Customer { FullName = invoice.Supplier?.CompanyName ?? "Tedarikçi" },
                TermsAndConditions = invoice.Notes ?? string.Empty,
                SubTotal = invoice.SubTotal,
                TotalTax = invoice.VatTotal,
                GrandTotal = invoice.GrandTotal
            };

            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    quote.Lines.Add(new QuoteLine
                    {
                        ProductName = line.ProductName,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal
                    });
                }
            }

            GenerateStandardQuote(quote, filePath);
        }
        #endregion

        #region İş Emri İş Akışı PDF Şablonları (Keşif / Teklif / Montaj)

        /// <summary>
        /// Keşif Raporu — fiyat içermez. Keşif verileri DiscoveryReport altından gelir.
        /// </summary>
        public void GenerateDiscoveryReportPdf(DiscoveryReportDto report, ServiceJobDocumentDto job, string filePath)
        {
            AuditSensitiveDocumentAccess("Keşif raporu", job.CustomerId);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "KEŞİF RAPORU", $"Takip No: #{job.Id}", job));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, report.TechnicianName ?? job.AssignedTechnician));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("MÜŞTERİ TALEBİ / İHTİYAÇ AÇIKLAMASI").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(job.Description) ? "Özel bir açıklama belirtilmedi." : job.Description).FontSize(9);
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("TEKNİSYENİN SAHADAKİ TEKNİK TESPİTLERİ").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(report.TechnicalNotes) ? "Saha tespit notu eklenmedi." : report.TechnicalNotes).FontSize(9);
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("ÖNERİLEN ÇÖZÜM").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(report.RecommendedSolution) ? "Öneri belirtilmedi." : report.RecommendedSolution).FontSize(9);
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("TAHMİNİ MALZEMELER").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(2);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Malzeme").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Not").Bold().FontColor(BrandColors.Primary);
                                    });
                                    if (report.Materials.Count == 0)
                                    {
                                        table.Cell().ColumnSpan(3).Padding(8).Text("Tahmini malzeme girilmedi.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var material in report.Materials)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(material.ProductName).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{material.Quantity:N0}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(material.Notes ?? "").FontSize(9);
                                        }
                                    }
                                });
                            });

                            if (report.PhotoPaths.Count > 0)
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("KEŞİF FOTOĞRAFLARI").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                    c1.Item().PaddingTop(6).Text(string.Join("\n", report.PhotoPaths)).FontSize(8).FontColor("#78909C");
                                });
                            }

                            col.Item().PaddingTop(10).Row(r => BuildSignatureBlock(r, "TEKNİSYEN İMZA", report.TechnicianName ?? "Teknisyen", "MÜŞTERİ ONAY / İMZA", job.CustomerName));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Keşif Raporu"));
                });
            })
            .GeneratePdf(filePath);
        }

        /// <summary>
        /// Fiyat Teklifi — malzeme, miktar, birim fiyat, iskonto, KDV, işçilik, nakliye ve ticari şartlar.
        /// </summary>
        public void GenerateWorkOrderQuotationPdf(WorkOrderQuotationDto quotation, ServiceJobDocumentDto job, string filePath)
        {
            AuditSensitiveDocumentAccess("Fiyat teklifi", job.CustomerId);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "FİYAT TEKLİFİ",
                        $"Teklif No: {quotation.QuotationNumber} | Takip No: #{job.Id}", job,
                        $"Tarih: {quotation.IssuedDate:dd MMMM yyyy} | Geçerlilik: {(quotation.ValidUntil.HasValue ? quotation.ValidUntil.Value.ToString("dd MMMM yyyy") : "-")}"));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, null));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("TEKLİF KALEMLERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Malzeme / Hizmet").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("İskonto").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Ara Toplam").Bold().FontColor(BrandColors.Primary);
                                    });
                                    if (quotation.Items.Count == 0)
                                    {
                                        table.Cell().ColumnSpan(5).Padding(8).Text("Teklif kalemi girilmedi.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var item in quotation.Items)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(item.ProductName).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.Quantity:N2}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.UnitPrice:C2}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.DiscountPercent:N2}%").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.LineTotal:C2}").FontSize(9);
                                        }
                                    }
                                });
                            });

                            col.Item().AlignRight().Width(280).Column(c1 =>
                            {
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Malzeme Ara Toplamı:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.Items.Sum(i => i.LineTotal):C2}").FontSize(9).Bold();
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("İskonto:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"-{quotation.DiscountAmount:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("İşçilik:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.LaborCost:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Nakliye:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.ShippingCost:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("KDV (satır bazlı):").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.TaxAmount:C2}").FontSize(9);
                                });
                                c1.Item().PaddingTop(6).LineHorizontal(1).LineColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text("GENEL TOPLAM:").FontSize(12).Bold().FontColor(BrandColors.Primary);
                                    r.RelativeItem().AlignRight().Text($"{quotation.TotalAmount:C2}").FontSize(12).Bold().FontColor(BrandColors.Secondary);
                                });
                            });

                            if (!string.IsNullOrWhiteSpace(quotation.Description) ||
                                !string.IsNullOrWhiteSpace(quotation.Warranty) ||
                                !string.IsNullOrWhiteSpace(quotation.DeliveryTime) ||
                                !string.IsNullOrWhiteSpace(quotation.PaymentTerms))
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("TİCARİ ŞARTLAR").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                    if (!string.IsNullOrWhiteSpace(quotation.Description))
                                        c1.Item().PaddingTop(5).Text($"Açıklama: {quotation.Description}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(quotation.Warranty))
                                        c1.Item().PaddingTop(3).Text($"Garanti: {quotation.Warranty}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(quotation.DeliveryTime))
                                        c1.Item().PaddingTop(3).Text($"Teslim Süresi: {quotation.DeliveryTime}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(quotation.PaymentTerms))
                                        c1.Item().PaddingTop(3).Text($"Ödeme Şartları: {quotation.PaymentTerms}").FontSize(9);
                                });
                            }

                            col.Item().PaddingTop(10).Row(r => BuildSignatureBlock(r, "TEKLİFİ HAZIRLAYAN", "", "MÜŞTERİ ONAY / İMZA", job.CustomerName));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Fiyat Teklifi"));
                });
            })
            .GeneratePdf(filePath);
        }

        /// <summary>
        /// Montaj İş Emri — teknisyen, montaj tarihi, malzemeler ve görevler.
        /// </summary>
        public void GenerateInstallationOrderPdf(InstallationOrderDto order, ServiceJobDocumentDto job, string filePath)
        {
            AuditSensitiveDocumentAccess("Montaj iş emri", job.CustomerId);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "MONTAJ İŞ EMRİ", $"Montaj No: #{order.Id} | Takip No: #{job.Id}", job,
                        $"Montaj Tarihi: {(order.InstallationDate.HasValue ? order.InstallationDate.Value.ToString("dd MMMM yyyy HH:mm") : "-")}"));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, order.TechnicianName));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("MONTAJ MALZEMELERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(2);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Malzeme").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Not").Bold().FontColor(BrandColors.Primary);
                                    });
                                    if (order.Materials.Count == 0)
                                    {
                                        table.Cell().ColumnSpan(3).Padding(8).Text("Montaj malzemesi girilmedi.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var material in order.Materials)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(material.ProductName).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{material.Quantity:N2}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(material.Notes ?? "").FontSize(9);
                                        }
                                    }
                                });
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("MONTAJ GÖREVLERİ").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(6).Column(c2 =>
                                {
                                    if (order.Tasks.Count == 0)
                                    {
                                        c2.Item().Text("Görev tanımlanmadı.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var task in order.Tasks)
                                        {
                                            c2.Item().PaddingTop(3).Row(r =>
                                            {
                                                r.ConstantItem(16).Text(task.IsCompleted ? "☑" : "☐").FontSize(10);
                                                r.RelativeItem().Text(task.Title + (string.IsNullOrWhiteSpace(task.Description) ? "" : $" — {task.Description}")).FontSize(9);
                                            });
                                        }
                                    }
                                });
                            });

                            if (!string.IsNullOrWhiteSpace(order.Notes))
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("MONTAJ NOTLARI").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                    c1.Item().PaddingTop(6).Text(order.Notes).FontSize(9);
                                });
                            }

                            col.Item().PaddingTop(10).Row(r => BuildSignatureBlock(r, "TEKNİSYEN İMZA", order.TechnicianName ?? "Teknisyen", "MÜŞTERİ ONAY / İMZA", job.CustomerName));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Montaj İş Emri"));
                });
            })
            .GeneratePdf(filePath);
        }

        /// <summary>
        /// Montaj Tamamlama Formu — gerçek kullanılan malzemeler, tamamlanma tarihi,
        /// teknisyen, teslim notu ve müşteri imzası.
        /// </summary>
        public void GenerateInstallationCompletionFormPdf(InstallationOrderDto order, ServiceJobDocumentDto job, string filePath)
        {
            AuditSensitiveDocumentAccess("Montaj tamamlama formu", job.CustomerId);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "MONTAJ TAMAMLAMA FORMU", $"Montaj No: #{order.Id} | Takip No: #{job.Id}", job,
                        $"Tamamlanma Tarihi: {(order.CompletedAt.HasValue ? order.CompletedAt.Value.ToString("dd MMMM yyyy HH:mm") : "-")}"));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, order.CompletionTechnician ?? order.TechnicianName));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("KULLANILAN MALZEMELER").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Malzeme").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary);
                                    });
                                    if (order.Materials.Count == 0)
                                    {
                                        table.Cell().ColumnSpan(3).Padding(8).Text("Kullanılan malzeme girilmedi.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var material in order.Materials)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(material.ProductName).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{material.Quantity:N0}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{material.UnitPrice:C2}").FontSize(9);
                                        }
                                    }
                                });
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("İŞÇİLİK VE TESLİM").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text("İşçilik Saati:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{order.LaborHours:N1} saat").FontSize(9).Bold();
                                });
                                if (!string.IsNullOrWhiteSpace(order.CompletionTechnician))
                                {
                                    c1.Item().PaddingTop(3).Row(r =>
                                    {
                                        r.RelativeItem().Text("Tamamlayan Teknisyen:").FontSize(9);
                                        r.RelativeItem().AlignRight().Text(order.CompletionTechnician).FontSize(9);
                                    });
                                }
                                if (!string.IsNullOrWhiteSpace(order.DeliveryNote))
                                {
                                    c1.Item().PaddingTop(8).Text("Teslim Notu:").FontSize(9).FontColor(BrandColors.Secondary);
                                    c1.Item().PaddingTop(3).Text(order.DeliveryNote).FontSize(9);
                                }
                            });

                            col.Item().PaddingTop(10).Row(r => BuildCompletionSignatureBlock(r, order, job));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Montaj Tamamlama Formu"));
                });
            })
            .GeneratePdf(filePath);
        }

        // ── IDiscoveryPdfService / IQuotationPdfService / IInstallationPdfService arayüz uygulamaları ──

        public void GenerateDiscoveryReportPdf(DiscoveryReport report, string filePath)
        {
            var dto = new DiscoveryReportDto(
                report.Id,
                report.ServiceJobId,
                report.TechnicalNotes,
                report.RecommendedSolution,
                report.PhotoPathsList,
                report.EstimatedLaborHours,
                report.TechnicianName,
                report.Materials.Select(m => new DiscoveryMaterialDto(m.Id, m.ProductId, m.ProductName, m.Quantity, m.Notes)).ToList());

            var job = new ServiceJobDocumentDto(
                report.ServiceJobId, WorkOrderType.Discovery, string.Empty, report.TechnicalNotes, null,
                report.TechnicianName, JobPriority.Normal, null, 0, "İş Emri", string.Empty, string.Empty, string.Empty);
            GenerateDiscoveryReportPdf(dto, job, filePath);
        }

        public void GenerateWorkOrderQuotationPdf(WorkOrderQuotation quotation, string filePath)
        {
            var dto = new WorkOrderQuotationDto(
                quotation.Id, quotation.ServiceJobId, quotation.QuotationNumber, quotation.Status,
                quotation.IssuedDate, quotation.ValidUntil, quotation.Description, quotation.Warranty,
                quotation.DeliveryTime, quotation.PaymentTerms, quotation.LaborCost, quotation.ShippingCost,
                quotation.DiscountAmount, quotation.TaxRate, quotation.TaxAmount, quotation.TotalAmount,
                quotation.SentDate, quotation.AcceptedAt, quotation.RejectedAt, quotation.RejectionReason,
                quotation.Items.OrderBy(i => i.Sequence).Select(i => new QuotationItemDto(
                    i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountPercent, i.TaxPercent, i.LineTotal, i.Sequence)).ToList(),
                quotation.RevisionNumber, quotation.ParentQuotationId);

            var job = new ServiceJobDocumentDto(
                quotation.ServiceJobId, WorkOrderType.Repair, quotation.Description ?? string.Empty, null, null,
                null, JobPriority.Normal, null, 0, "İş Emri", string.Empty, string.Empty, string.Empty);
            GenerateWorkOrderQuotationPdf(dto, job, filePath);
        }

        public void GenerateInstallationOrderPdf(InstallationOrder order, string filePath)
        {
            var dto = ToInstallationOrderDto(order);
            var job = new ServiceJobDocumentDto(
                order.ServiceJobId, WorkOrderType.Installation, string.Empty, null, null,
                order.TechnicianName, JobPriority.Normal, order.InstallationDate, 0, "İş Emri", string.Empty, string.Empty, string.Empty);
            GenerateInstallationOrderPdf(dto, job, filePath);
        }

        public void GenerateInstallationCompletionFormPdf(InstallationOrder order, string filePath)
        {
            var dto = ToInstallationOrderDto(order);
            var job = new ServiceJobDocumentDto(
                order.ServiceJobId, WorkOrderType.Installation, string.Empty, null, null,
                order.CompletionTechnician ?? order.TechnicianName, JobPriority.Normal, order.CompletedAt, 0,
                "İş Emri", string.Empty, string.Empty, string.Empty);
            GenerateInstallationCompletionFormPdf(dto, job, filePath);
        }

        private static InstallationOrderDto ToInstallationOrderDto(InstallationOrder order) => new(
            order.Id, order.ServiceJobId, order.QuotationId, order.TechnicianId, order.TechnicianName,
            order.InstallationDate, order.Notes, order.LaborHours, order.CompletedAt, order.CompletionTechnician,
            order.DeliveryNote, order.CustomerSignature,
            order.Materials.Select(m => new InstallationMaterialDto(m.Id, m.ProductId, m.ProductName, m.Quantity, m.UnitPrice, m.Notes)).ToList(),
            order.Tasks.Select(t => new InstallationTaskDto(t.Id, t.Title, t.Description, t.IsCompleted, t.CompletedAt)).ToList());

        // ── Teslim & Faturalandırma (Paket 7) ──

        /// <summary>
        /// İş emri faturası — kabul edilen teklif kalemleri, işçilik, nakliye, iskonto ve KDV
        /// üzerinden üretilir; teslim/ödeme bilgileri (durum, yöntem, tahsilat, bakiye, fatura no)
        /// faturanın altında gösterilir.
        /// </summary>
        public void GenerateWorkOrderInvoice(WorkOrderWorkflowDto workflow, ServiceJobDocumentDto job, string filePath)
        {
            var quotation = workflow.Quotation
                ?? throw new ArgumentException("Fatura üretimi için teklif kaydı gerekir.");

            AuditSensitiveDocumentAccess("Fatura", job.CustomerId);
            var logoBytes = GetLogoBytes();
            var delivery = workflow.Delivery;
            var invoiceNumber = string.IsNullOrWhiteSpace(delivery?.InvoiceNumber)
                ? $"INV-{workflow.JobId:D6}"
                : delivery!.InvoiceNumber;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "FATURA",
                        $"Fatura No: {invoiceNumber} | Takip No: #{job.Id}", job,
                        $"Tarih: {DateTime.Now:dd MMMM yyyy}"));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(15);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, job.AssignedTechnician));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("FATURA KALEMLERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                        cols.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).Text("Malzeme / Hizmet").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("İskonto").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("KDV %").Bold().FontColor(BrandColors.Primary);
                                        header.Cell().Background(BrandColors.TableHeader).Padding(6).AlignRight().Text("Tutar").Bold().FontColor(BrandColors.Primary);
                                    });
                                    if (quotation.Items.Count == 0)
                                    {
                                        table.Cell().ColumnSpan(6).Padding(8).Text("Fatura kalemi bulunamadı.").FontSize(9).FontColor(BrandColors.TextSecondary);
                                    }
                                    else
                                    {
                                        foreach (var item in quotation.Items)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).Text(item.ProductName).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.Quantity:N2}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.UnitPrice:C2}").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.DiscountPercent:N2}%").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.TaxPercent:N2}%").FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(6).AlignRight().Text($"{item.LineTotal:C2}").FontSize(9);
                                        }
                                    }
                                });
                            });

                            col.Item().AlignRight().Width(280).Column(c1 =>
                            {
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Malzeme Ara Toplamı:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.Items.Sum(i => i.LineTotal):C2}").FontSize(9).Bold();
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("İskonto:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"-{quotation.DiscountAmount:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("İşçilik:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.LaborCost:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Nakliye:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.ShippingCost:C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("KDV (satır bazlı):").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{quotation.TaxAmount:C2}").FontSize(9);
                                });
                                c1.Item().PaddingTop(6).LineHorizontal(1).LineColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(6).Row(r =>
                                {
                                    r.RelativeItem().Text("GENEL TOPLAM:").FontSize(12).Bold().FontColor(BrandColors.Primary);
                                    r.RelativeItem().AlignRight().Text($"{quotation.TotalAmount:C2}").FontSize(12).Bold().FontColor(BrandColors.Secondary);
                                });
                            });

                            col.Item().Border(1).BorderColor("#E0E0E0").Background("#F8F9FA").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("ÖDEME BİLGİLERİ").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                c1.Item().PaddingTop(5).Row(r =>
                                {
                                    r.RelativeItem().Text("Ödeme Durumu:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text(PaymentStatusLabels.Map(delivery?.PaymentStatus ?? PaymentStatus.Unpaid)).FontSize(9).Bold();
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Ödeme Yöntemi:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text(PaymentMethodLabels.Map(delivery?.PaymentMethod ?? PaymentMethod.Cash)).FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Tahsil Edilen:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{(delivery?.PaidAmount ?? 0m):C2}").FontSize(9);
                                });
                                c1.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Kalan Bakiye:").FontSize(9);
                                    r.RelativeItem().AlignRight().Text($"{Math.Max(0m, quotation.TotalAmount - (delivery?.PaidAmount ?? 0m)):C2}").FontSize(9).Bold();
                                });
                            });

                            col.Item().PaddingTop(10).Row(r => BuildSignatureBlock(
                                r, "FATURAYI DÜZENLEYEN", job.AssignedTechnician ?? "", "MÜŞTERİ İMZASI", job.CustomerName));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Fatura"));
                });
            })
            .GeneratePdf(filePath);
        }

        /// <summary>
        /// Servis / İş Formu — iş künyesi, keşif özeti, montaj özeti ve teslim/ödeme bilgileri
        /// tek belgede birleştirilir (masaüstü workspace'ten üretilir).
        /// </summary>
        public void GenerateWorkOrderServiceReport(WorkOrderWorkflowDto workflow, ServiceJobDocumentDto job, string filePath)
        {
            AuditSensitiveDocumentAccess("Servis raporu", job.CustomerId);
            var logoBytes = GetLogoBytes();
            var delivery = workflow.Delivery;
            var installation = workflow.Installation;
            var discovery = workflow.Discovery;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => BuildWorkOrderHeader(c, logoBytes, "SERVİS / İŞ FORMU",
                        $"Takip No: #{job.Id}", job,
                        $"Tarih: {DateTime.Now:dd MMMM yyyy}"));

                    page.Content().Element(c =>
                    {
                        c.PaddingTop(15).Column(col =>
                        {
                            col.Spacing(12);

                            col.Item().Element(c1 => BuildWorkOrderCustomerBox(c1, job, job.AssignedTechnician));

                            col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                            {
                                c1.Item().Text("İŞ BİLGİLERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                c1.Item().PaddingTop(5).Text($"Açıklama: {(string.IsNullOrWhiteSpace(job.Description) ? "Belirtilmedi" : job.Description)}").FontSize(9);
                                c1.Item().Text($"Öncelik: {job.Priority}   |   Planlanan: {(job.ScheduledDate.HasValue ? job.ScheduledDate.Value.ToString("dd MMMM yyyy HH:mm") : "Belirtilmedi")}").FontSize(9);
                                c1.Item().Text($"İş Durumu: {ServiceJobRowDto.MapStatusDisplay(workflow.JobStatus)}").FontSize(9);
                            });

                            if (discovery is not null)
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("KEŞİF ÖZETİ").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                    if (!string.IsNullOrWhiteSpace(discovery.TechnicalNotes))
                                        c1.Item().PaddingTop(5).Text($"Teknik Tespitler: {discovery.TechnicalNotes}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(discovery.RecommendedSolution))
                                        c1.Item().PaddingTop(3).Text($"Önerilen Çözüm: {discovery.RecommendedSolution}").FontSize(9);
                                    c1.Item().PaddingTop(3).Text($"Tahmini Malzeme: {discovery.Materials.Count} kalem   |   Tahmini İşçilik: {discovery.EstimatedLaborHours:N1} saat").FontSize(9);
                                });
                            }

                            if (installation is not null)
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("MONTAJ ÖZETİ").FontSize(11).Bold().FontColor(BrandColors.Secondary);
                                    c1.Item().PaddingTop(5).Text($"Teknisyen: {installation.TechnicianName ?? "Atanmadı"}   |   Tarih: {(installation.InstallationDate.HasValue ? installation.InstallationDate.Value.ToString("dd MMMM yyyy") : "Planlanmadı")}").FontSize(9);
                                    c1.Item().Text($"İşçilik: {installation.LaborHours:N1} saat   |   Durum: {(installation.CompletedAt.HasValue ? "Tamamlandı" : "Devam Ediyor")}").FontSize(9);
                                    if (installation.Materials.Count > 0)
                                    {
                                        c1.Item().PaddingTop(5).Table(table =>
                                        {
                                            table.ColumnsDefinition(cols =>
                                            {
                                                cols.RelativeColumn(3);
                                                cols.RelativeColumn(1);
                                                cols.RelativeColumn(1);
                                            });
                                            table.Header(header =>
                                            {
                                                header.Cell().Background(BrandColors.TableHeader).Padding(5).Text("Kullanılan Malzeme").Bold().FontColor(BrandColors.Primary).FontSize(9);
                                                header.Cell().Background(BrandColors.TableHeader).Padding(5).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary).FontSize(9);
                                                header.Cell().Background(BrandColors.TableHeader).Padding(5).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary).FontSize(9);
                                            });
                                            foreach (var material in installation.Materials)
                                            {
                                                table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(5).Text(material.ProductName).FontSize(8.5f);
                                                table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(5).AlignRight().Text($"{material.Quantity:N2}").FontSize(8.5f);
                                                table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(5).AlignRight().Text($"{material.UnitPrice:C2}").FontSize(8.5f);
                                            }
                                        });
                                    }
                                    if (!string.IsNullOrWhiteSpace(installation.DeliveryNote))
                                        c1.Item().PaddingTop(5).Text($"Teslim Notu: {installation.DeliveryNote}").FontSize(9);
                                });
                            }

                            if (delivery is not null)
                            {
                                col.Item().Border(1).BorderColor("#E0E0E0").Padding(12).Column(c1 =>
                                {
                                    c1.Item().Text("TESLİM VE ÖDEME").FontSize(11).Bold().FontColor(BrandColors.Primary);
                                    c1.Item().PaddingTop(5).Text($"Teslim Tarihi: {delivery.DeliveryDate:dd MMMM yyyy HH:mm}   |   Teslim Eden: {delivery.DeliveredBy ?? "—"}").FontSize(9);
                                    c1.Item().Text($"Ödeme: {PaymentStatusLabels.Map(delivery.PaymentStatus)} ({PaymentMethodLabels.Map(delivery.PaymentMethod)}) — Tahsilat: {delivery.PaidAmount:C2}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(delivery.InvoiceNumber))
                                        c1.Item().Text($"Fatura No: {delivery.InvoiceNumber}").FontSize(9);
                                    if (!string.IsNullOrWhiteSpace(delivery.DeliveryNote))
                                        c1.Item().Text($"Teslim Notu: {delivery.DeliveryNote}").FontSize(9);
                                });
                            }

                            col.Item().PaddingTop(10).Row(r => BuildSignatureBlock(
                                r, "TEKNİSYEN", job.AssignedTechnician ?? "", "MÜŞTERİ İMZASI", job.CustomerName));
                        });
                    });

                    page.Footer().Element(c => BuildWorkOrderFooter(c, "Kamatek CRM Servis Raporu"));
                });
            })
            .GeneratePdf(filePath);
        }

        // ── Ortak Başlık / Alt Bilgi / Blok Yardımcıları ──

        private void BuildWorkOrderHeader(IContainer container, byte[]? logoBytes, string title, string subtitle, ServiceJobDocumentDto job, string? extraLine = null)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(r =>
                    {
                        if (logoBytes != null)
                        {
                            r.Item().Width(200).Image(logoBytes).FitArea();
                        }
                        else
                        {
                            r.Item().Text("KAMATEK").FontSize(28).Bold().FontColor(BrandColors.Primary);
                            r.Item().Text("ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(9).FontColor(BrandColors.Secondary);
                        }
                    });

                    row.ConstantItem(250).AlignRight().Column(r =>
                    {
                        r.Item().Text(title).FontSize(16).Bold().FontColor(BrandColors.Primary);
                        r.Item().Text(subtitle).FontSize(10).Bold().FontColor(BrandColors.Secondary);
                        r.Item().Text($"Tarih: {DateTime.Now:dd MMMM yyyy}").FontSize(9).FontColor(BrandColors.TextSecondary);
                        if (!string.IsNullOrWhiteSpace(extraLine))
                        {
                            r.Item().Text(extraLine).FontSize(9).FontColor(BrandColors.TextSecondary);
                        }
                    });
                });

                col.Item().PaddingTop(10).LineHorizontal(2).LineColor(BrandColors.Secondary);
            });
        }

        private void BuildWorkOrderCustomerBox(IContainer container, ServiceJobDocumentDto job, string? technician)
        {
            container.Border(1).BorderColor("#E0E0E0").Background("#F8F9FA").Padding(12).Column(c1 =>
            {
                c1.Item().Text("MÜŞTERİ VE SAHA BİLGİLERİ").FontSize(11).Bold().FontColor(BrandColors.Primary);
                c1.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Column(c2 =>
                    {
                        c2.Item().Text($"Müşteri / Firma: {job.CustomerName}").Bold().FontSize(9);
                        if (!string.IsNullOrWhiteSpace(job.CustomerCompanyName))
                            c2.Item().Text($"Firma: {job.CustomerCompanyName}").FontSize(9);
                        c2.Item().Text($"Telefon: {Protect(job.CustomerPhone, PersonalDataKind.Phone)}").FontSize(9);
                        c2.Item().Text($"Adres: {Protect(job.CustomerAddress, PersonalDataKind.Address)}").FontSize(9);
                    });
                    r.RelativeItem().Column(c3 =>
                    {
                        c3.Item().Text($"Teknisyen: {technician ?? "Atanmadı"}").FontSize(9);
                        c3.Item().Text($"Öncelik: {job.Priority}").FontSize(9);
                        c3.Item().Text($"İş: #{job.Id}").FontSize(9);
                    });
                });
            });
        }

        private static void BuildSignatureBlock(QuestPDF.Fluent.RowDescriptor left, string leftTitle, string leftName, string rightTitle, string rightName)
        {
            left.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
            {
                c1.Item().Text(leftTitle).FontSize(9).Bold().FontColor(BrandColors.Primary);
                c1.Item().PaddingTop(30).Text(leftName).FontSize(8).AlignRight();
            });
            left.ConstantItem(20);
            left.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
            {
                c1.Item().Text(rightTitle).FontSize(9).Bold().FontColor(BrandColors.Primary);
                c1.Item().PaddingTop(30).Text(rightName).FontSize(8).AlignRight();
            });
        }

        private static void BuildCompletionSignatureBlock(QuestPDF.Fluent.RowDescriptor left, InstallationOrderDto order, ServiceJobDocumentDto job)
        {
            left.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
            {
                c1.Item().Text("TEKNİSYEN İMZA").FontSize(9).Bold().FontColor(BrandColors.Primary);
                c1.Item().PaddingTop(30).Text(order.CompletionTechnician ?? order.TechnicianName ?? "Teknisyen").FontSize(8).AlignRight();
            });
            left.ConstantItem(20);
            left.RelativeItem().Border(1).BorderColor("#E0E0E0").Padding(10).Column(c1 =>
            {
                c1.Item().Text("MÜŞTERİ İMZASI").FontSize(9).Bold().FontColor(BrandColors.Primary);
                if (!string.IsNullOrWhiteSpace(order.CustomerSignature))
                {
                    try
                    {
                        var signatureBytes = Convert.FromBase64String(order.CustomerSignature);
                        c1.Item().PaddingTop(8).Width(150).Height(50).Image(signatureBytes).FitArea();
                    }
                    catch
                    {
                        c1.Item().PaddingTop(30).Text(job.CustomerName).FontSize(8).AlignRight();
                    }
                }
                else
                {
                    c1.Item().PaddingTop(30).Text(job.CustomerName).FontSize(8).AlignRight();
                }
            });
        }

        private static void BuildWorkOrderFooter(IContainer container, string label)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor("#E0E0E0");
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text(label).FontSize(8).FontColor("#9E9E9E");
                    r.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Sayfa ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        }

        #region Customer Account Statement (Cari Ekstre)

        public void GenerateCustomerStatement(Customer customer, IEnumerable<Transaction> transactions, decimal currentBalance, string filePath)
        {
            AuditSensitiveDocumentAccess("Cari Hesap Ekstresi", customer?.Id);
            var logoBytes = GetLogoBytes();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            if (logoBytes != null)
                            {
                                row.ConstantItem(120).Height(50).Image(logoBytes).FitArea();
                            }
                            else
                            {
                                row.RelativeItem().Text("KAMATEK GÜVENLİK VE BİLİŞİM").FontSize(16).Bold().FontColor(BrandColors.Primary);
                            }

                            row.RelativeItem().Column(rCol =>
                            {
                                rCol.Item().AlignRight().Text("MÜŞTERİ CARİ HESAP EKSTRESİ").FontSize(14).Bold().FontColor(BrandColors.Primary);
                                rCol.Item().AlignRight().Text($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor(BrandColors.TextSecondary);
                            });
                        });

                        col.Item().PaddingVertical(10).LineHorizontal(1.5f).LineColor(BrandColors.Primary);

                        // Müşteri Bilgileri
                        col.Item().Border(1).BorderColor("#E0E0E0").Background(BrandColors.LightGray).Padding(10).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Müşteri: {customer.FullName}").Bold().FontSize(11).FontColor(BrandColors.Primary);
                                c.Item().Text($"Müşteri Kodu: {customer.CustomerCode}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(customer.CompanyName))
                                    c.Item().Text($"Firma: {customer.CompanyName}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(customer.TaxNumber))
                                    c.Item().Text($"Vergi No / Daire: {Protect(customer.TaxNumber, PersonalDataKind.TaxNumber)} / {customer.TaxOffice}").FontSize(9);
                            });

                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Telefon: {Protect(customer.PhoneNumber, PersonalDataKind.Phone)}").FontSize(9);
                                c.Item().Text($"E-posta: {Protect(customer.Email, PersonalDataKind.Email)}").FontSize(9);
                                c.Item().Text($"Adres: {Protect(customer.FullAddress, PersonalDataKind.Address)}").FontSize(9);
                            });
                        });

                        col.Item().PaddingVertical(10);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);  // Tarih
                                columns.ConstantColumn(90);  // İşlem Tipi
                                columns.RelativeColumn();    // Açıklama
                                columns.ConstantColumn(85);  // Borç (+)
                                columns.ConstantColumn(85);  // Alacak (-)
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(BrandColors.TableHeader).Padding(5).Text("Tarih").Bold().FontSize(9);
                                header.Cell().Background(BrandColors.TableHeader).Padding(5).Text("İşlem Tipi").Bold().FontSize(9);
                                header.Cell().Background(BrandColors.TableHeader).Padding(5).Text("Açıklama").Bold().FontSize(9);
                                header.Cell().Background(BrandColors.TableHeader).Padding(5).AlignRight().Text("Borç").Bold().FontSize(9);
                                header.Cell().Background(BrandColors.TableHeader).Padding(5).AlignRight().Text("Tahsilat").Bold().FontSize(9);
                            });

                            decimal totalDebt = 0;
                            decimal totalCredit = 0;

                            foreach (var item in transactions.OrderBy(t => t.Date))
                            {
                                bool isDebt = item.Type == TransactionType.Debt || item.Type == TransactionType.Refund;
                                decimal debtAmount = isDebt ? item.Amount : 0;
                                decimal creditAmount = !isDebt ? item.Amount : 0;
                                totalDebt += debtAmount;
                                totalCredit += creditAmount;

                                string typeName = item.Type switch
                                {
                                    TransactionType.Payment => "Tahsilat",
                                    TransactionType.Debt => "Borç",
                                    TransactionType.Refund => "İade",
                                    TransactionType.CreditNote => "Alacak Dekontu",
                                    _ => item.Type.ToString()
                                };

                                table.Cell().BorderBottom(0.5f).BorderColor("#E0E0E0").Padding(4).Text($"{item.Date:dd.MM.yyyy}").FontSize(8.5f);
                                table.Cell().BorderBottom(0.5f).BorderColor("#E0E0E0").Padding(4).Text(typeName).FontSize(8.5f);
                                table.Cell().BorderBottom(0.5f).BorderColor("#E0E0E0").Padding(4).Text(item.Description ?? "-").FontSize(8.5f);
                                table.Cell().BorderBottom(0.5f).BorderColor("#E0E0E0").Padding(4).AlignRight().Text(debtAmount > 0 ? $"{debtAmount:C2}" : "-").FontSize(8.5f);
                                table.Cell().BorderBottom(0.5f).BorderColor("#E0E0E0").Padding(4).AlignRight().Text(creditAmount > 0 ? $"{creditAmount:C2}" : "-").FontSize(8.5f);
                            }

                            // Toplam Satırı
                            table.Cell().ColumnSpan(3).Background(BrandColors.LightGray).Padding(6).AlignRight().Text("TOPLAMLAR:").Bold().FontSize(9);
                            table.Cell().Background(BrandColors.LightGray).Padding(6).AlignRight().Text($"{totalDebt:C2}").Bold().FontSize(9);
                            table.Cell().Background(BrandColors.LightGray).Padding(6).AlignRight().Text($"{totalCredit:C2}").Bold().FontSize(9);
                        });

                        col.Item().PaddingTop(15).AlignRight().Column(bCol =>
                        {
                            bCol.Item().Row(r =>
                            {
                                r.ConstantItem(120).Text("Toplam Borç:").FontSize(9);
                                r.ConstantItem(100).AlignRight().Text($"{transactions.Where(t => t.Type == TransactionType.Debt || t.Type == TransactionType.Refund).Sum(t => t.Amount):C2}").FontSize(9);
                            });
                            bCol.Item().Row(r =>
                            {
                                r.ConstantItem(120).Text("Toplam Tahsilat:").FontSize(9);
                                r.ConstantItem(100).AlignRight().Text($"{transactions.Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.CreditNote).Sum(t => t.Amount):C2}").FontSize(9);
                            });
                            bCol.Item().PaddingTop(4).BorderTop(1).BorderColor(BrandColors.Primary).Row(r =>
                            {
                                r.ConstantItem(120).Text("GÜNCEL BAKİYE:").Bold().FontSize(11).FontColor(BrandColors.Primary);
                                string balanceSign = currentBalance > 0 ? "(Borçlu)" : (currentBalance < 0 ? "(Alacaklı)" : "(Sıfır)");
                                r.ConstantItem(140).AlignRight().Text($"{Math.Abs(currentBalance):C2} {balanceSign}").Bold().FontSize(11).FontColor(currentBalance > 0 ? BrandColors.Danger : (currentBalance < 0 ? BrandColors.Success : BrandColors.TextSecondary));
                            });
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor("#E0E0E0");
                        col.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("Kamatek CRM Finans Yönetim Sistemi").FontSize(8).FontColor("#9E9E9E");
                            r.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Sayfa ");
                                x.CurrentPageNumber();
                                x.Span(" / ");
                                x.TotalPages();
                            });
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        #region Sample Live Preview

        /// <summary>
        /// Canlı önizleme için geçici bir örnek teklif PDF'i üretir ve dosya yolunu döner.
        /// </summary>
        public string GenerateSampleQuotePreviewPdf(CompanyBrandProfile? customProfile = null, string? outputPath = null)
        {
            var brand = customProfile ?? AppSettings.GetBrandProfile();
            var targetPath = outputPath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), "KamatekCRM_Previews");
                if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);
                targetPath = Path.Combine(tempFolder, $"Teklif_Onizleme_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            var sampleQuote = new Quote
            {
                QuoteNumber = "TEK-" + DateTime.Now.ToString("yyyyMMdd") + "-001",
                Date = DateTime.Now,
                ValidUntil = DateTime.Now.AddDays(brand.QuoteValidityDays > 0 ? brand.QuoteValidityDays : 7),
                Customer = new Customer
                {
                    FullName = "Örnek Müşteri / Referans Firma A.Ş.",
                    PhoneNumber = "0532 555 44 33",
                    Email = "bilgi@ornekmusteri.com",
                    City = "Eskişehir",
                    District = "Odunpazarı",
                    Street = "Atatürk Bulvarı No: 124/B"
                },
                TermsAndConditions = brand.QuoteTermsText,
                Lines = new List<QuoteLine>
                {
                    new QuoteLine
                    {
                        ProductName = "4 MP Smart Hibrit LED Aydınlatmalı IP Bullet Kamera",
                        ProductCode = "IPC-HFW2449S",
                        Quantity = 8,
                        Unit = "Adet",
                        UnitPrice = 2450.00m,
                        DiscountPercent = 10,
                        LineTotal = 17640.00m
                    },
                    new QuoteLine
                    {
                        ProductName = "8 Kanal 4K H.265+ Ağ Video Kayıt Cihazı (NVR)",
                        ProductCode = "NVR-4108HS-4KS2",
                        Quantity = 1,
                        Unit = "Adet",
                        UnitPrice = 4850.00m,
                        DiscountPercent = 5,
                        LineTotal = 4607.50m
                    },
                    new QuoteLine
                    {
                        ProductName = "2 TB 7/24 Surveillance Güvenlik Diski",
                        ProductCode = "ST2000VX015",
                        Quantity = 1,
                        Unit = "Adet",
                        UnitPrice = 3200.00m,
                        DiscountPercent = 0,
                        LineTotal = 3200.00m
                    },
                    new QuoteLine
                    {
                        ProductName = "Profesyonel CCTV Montaj, Kablolama ve Devreye Alma",
                        ProductCode = "SRV-MONTAJ",
                        Quantity = 8,
                        Unit = "Nokta",
                        UnitPrice = 650.00m,
                        DiscountPercent = 0,
                        LineTotal = 5200.00m
                    }
                }
            };

            sampleQuote.SubTotal = sampleQuote.Lines.Sum(l => l.Quantity * l.UnitPrice);
            sampleQuote.TotalDiscount = sampleQuote.Lines.Sum(l => l.Quantity * l.UnitPrice * (l.DiscountPercent / 100m));
            decimal net = sampleQuote.SubTotal - sampleQuote.TotalDiscount;
            sampleQuote.TotalTax = net * 0.20m;
            sampleQuote.GrandTotal = net + sampleQuote.TotalTax;

            GenerateStandardQuote(sampleQuote, targetPath);
            return targetPath;
        }

        #endregion

        #endregion

        #endregion

        #endregion

        #endregion
    }
}
