using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.WorkOrders;
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
            var result = _auditTrail.WriteAsync(
                AuditActionType.View,
                "CustomerDocument",
                customerId?.ToString(),
                $"{documentType} oluşturma sırasında müşteri iletişim verilerine erişildi.").GetAwaiter().GetResult();
            if (result.IsFailure)
                System.Diagnostics.Debug.WriteLine(result.Error);
        }

        private static class BrandColors
        {
            public static string Primary = "#1A237E"; // Dark Navy Blue
            public static string Secondary = "#C61F25"; // Kamatek Red
            public static string Accent = "#C61F25";
            public static string TextPrimary = "#1A237E";
            public static string TextSecondary = "#757575";
            public static string LightGray = "#F5F5F5";
            public static string TableHeader = "#E8EAF6";
            public static string Success = "#4CAF50";
            public static string Warning = "#FF9800";
            public static string Danger = "#F44336";
        }

        private byte[]? GetLogoBytes()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Images/KamatekLogo.png");
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using var ms = new MemoryStream();
                    streamInfo.Stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch { }

            var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "KamatekLogo.png");
            var jpgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "KamatekLogo.jpg");

            if (File.Exists(pngPath)) return File.ReadAllBytes(pngPath);
            if (File.Exists(jpgPath)) return File.ReadAllBytes(jpgPath);
            
            return null;
        }

        public void GenerateProjectQuote(ServiceProject project, List<ScopeNode> rootNodes, string filePath)
        {
            AuditSensitiveDocumentAccess("Proje teklifi", project.Customer?.Id);
            var logoBytes = GetLogoBytes();

            var flattenedItems = FlattenScopeNodesWithImages(rootNodes);
            var totalAmount = flattenedItems.Where(i => !i.IsSectionHeader).Sum(i => i.TotalPrice);
            var totalItems = flattenedItems.Where(i => !i.IsSectionHeader).Sum(i => i.Quantity);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header().Element(c => ComposeProfessionalHeader(c, project, logoBytes, totalAmount));
                    page.Content().Element(c => ComposeProfessionalContent(c, project, flattenedItems, totalAmount, totalItems));
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
                        if (logoBytes != null)
                        {
                            c.Item().Width(240).Image(logoBytes).FitArea();
                        }
                        else
                        {
                            c.Item().Text("KAMATEK").FontSize(32).Bold().FontColor(BrandColors.Primary);
                            c.Item().Text("ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(10).FontColor(BrandColors.Secondary);
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

        private void ComposeProfessionalContent(IContainer container, ServiceProject project, List<PdfLineItem> items, decimal totalAmount, int totalItems)
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

                // 5. Malzeme Listesi (Fotoğraflı)
                col.Item().Element(c => ComposeProductTable(c, items));

                // 6. Finansal Özet
                col.Item().Element(c => ComposeFinancialSummary(c, items));

                // 7. Ticari Şartlar
                col.Item().Element(c => ComposeCommercialTerms(c));

                // 8. İmza Bloğu
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
            container.Background("#FFFFFF").BorderLeft(4).BorderColor(BrandColors.Secondary).BorderTop(1).BorderRight(1).BorderBottom(1).BorderColor("#E0E0E0").Padding(15).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("KAMATEK ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(12).Bold().FontColor(BrandColors.Primary);
                        col.Item().Text("Eskişehir Diafon Merkezi").FontSize(9).FontColor(BrandColors.Secondary).Italic();
                    });
                });
                c.Item().PaddingTop(8).Row(r => 
                {
                    r.RelativeItem().Column(col => 
                    {
                        col.Item().Text("📍 Kurtuluş, Ziya Paşa Cd. 72/A Odunpazarı / Eskişehir").FontSize(9).FontColor(BrandColors.TextSecondary);
                        col.Item().PaddingTop(2).Text("📞 +90 222 240 4060  |  📱 +90 545 545 8226").FontSize(9).Bold().FontColor(BrandColors.Primary);
                    });
                    
                    r.RelativeItem().AlignRight().Column(col => 
                    {
                        col.Item().AlignRight().Text("✉️ info@kamatekelektrik.com").FontSize(9);
                        col.Item().AlignRight().Text("🌐 www.kamatekelektrik.com").FontSize(9).FontColor(BrandColors.Primary);
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
                            r.ConstantItem((int)barWidth).Background(BrandColors.Secondary).Height(15);
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

                var installment1 = totalAmount * 0.50m;
                var installment2 = totalAmount * 0.50m;

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
                c.Item().Text("MALZEME VE HİZMET LİSTESİ").FontSize(12).Bold().FontColor(BrandColors.Primary);

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
                        header.Cell().Background(BrandColors.Primary).Padding(5).Text("Ürün / Açıklama").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Miktar").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("B.Fiyat").FontColor(Colors.White).FontSize(8);
                        header.Cell().Background(BrandColors.Primary).Padding(5).AlignRight().Text("Toplam").FontColor(Colors.White).FontSize(8);
                    });

                    int index = 1;
                    var productItems = items.Where(i => !i.IsSectionHeader).ToList();

                    foreach (var item in productItems)
                    {
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

        private void ComposeProductImage(IContainer container, string? imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(imagePath);
                    container.Image(bytes).FitArea();
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

        private void ComposeFinancialSummary(IContainer container, List<PdfLineItem> items)
        {
            var subTotal = items.Where(i => !i.IsSectionHeader).Sum(i => i.TotalPrice);
            var vatTotal = subTotal * 0.20m;
            var grandTotal = subTotal + vatTotal;

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

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("KDV (%20):").FontSize(11).FontColor(BrandColors.TextSecondary);
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
            container.Column(col =>
            {
                col.Item().Text("TİCARİ ŞARTLAR VE GARANTİ").FontSize(12).Bold().FontColor(BrandColors.Primary);

                col.Item().PaddingTop(8).Background("#FFF8E1").Border(1).BorderColor("#FFECB3").Padding(12).Column(c =>
                {
                    c.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text("✓").FontColor(BrandColors.Success);
                        row.RelativeItem().Text("Teklif Geçerlilik Süresi: 15 gün").FontSize(9);
                    });
                    c.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text("✓").FontColor(BrandColors.Success);
                        row.RelativeItem().Text("Garanti Süresi: 2 yıl (malzeme) + 1 yıl (işçilik)").FontSize(9);
                    });
                    c.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text("✓").FontColor(BrandColors.Success);
                        row.RelativeItem().Text("Teslim Süresi: Sipariş onayından itibaren 7-15 iş günü").FontSize(9);
                    });
                    c.Item().Row(row =>
                    {
                        row.ConstantItem(20).Text("✓").FontColor(BrandColors.Success);
                        row.RelativeItem().Text("Ödeme: %50 peşin, %50 teslimde").FontSize(9);
                    });
                });
            });
        }

        private void ComposeSignatures(IContainer container)
        {
            container.PaddingTop(30).Column(col =>
            {
                col.Item().Text("ONAY").FontSize(12).Bold().FontColor(BrandColors.Primary).AlignCenter();
                
                col.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Border(1).BorderColor("#CCCCCC").Height(60).AlignCenter()
                            .Text("MÜŞTERİ İMZA VE KAŞE").FontSize(9).FontColor(BrandColors.Primary);
                        c.Item().PaddingTop(5).Text("Tarih: ....................").FontSize(9);
                    });

                    row.ConstantItem(50);

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Border(1).BorderColor("#CCCCCC").Height(60).AlignCenter()
                            .Text("KAMATEK İMZA VE KAŞE").FontSize(9).FontColor(BrandColors.Secondary);
                        c.Item().PaddingTop(5).Text("Tarih: ....................").FontSize(9);
                    });
                });
            });
        }

        #endregion

        #region Professional Footer

        private void ComposeProfessionalFooter(IContainer container)
        {
            container.Column(col =>
            {
                // İnce Kırmızı Çizgi
                col.Item().LineHorizontal(2).LineColor(BrandColors.Secondary);

                col.Item().Background(BrandColors.Primary).Padding(15).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("KAMATEK ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(9).Bold().FontColor(Colors.White);
                        c.Item().Text("Kurtuluş, Ziya Paşa Cd. 72/A Odunpazarı/Eskişehir").FontSize(8).FontColor("#B0BEC5");
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("Bu doküman sistem üzerinden otomatik oluşturulmuştur.").FontSize(7).FontColor("#B0BEC5");
                        c.Item().AlignRight().Text(text =>
                        {
                            text.Span("Sayfa ").FontSize(8).FontColor(Colors.White);
                            text.CurrentPageNumber().FontSize(8).FontColor(Colors.White);
                            text.Span(" / ").FontSize(8).FontColor(Colors.White);
                            text.TotalPages().FontSize(8).FontColor(Colors.White);
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
                                if (logoBytes != null)
                                {
                                    c.Item().Width(240).Image(logoBytes).FitArea();
                                }
                                else 
                                {
                                    c.Item().Text("KAMATEK").FontSize(32).Bold().FontColor(BrandColors.Primary);
                                    c.Item().Text("ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(10).FontColor(BrandColors.Secondary);
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
                        if (logoBytes != null)
                        {
                            c.Item().Width(240).Image(logoBytes).FitArea();
                        }
                        else
                        {
                            c.Item().Text("KAMATEK").FontSize(32).Bold().FontColor(BrandColors.Primary);
                            c.Item().Text("ELEKTRİK VE GÜVENLİK SİSTEMLERİ").FontSize(10).FontColor(BrandColors.Secondary);
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
            container.Padding(20).Column(col =>
            {
                col.Spacing(15);

                // 1. Şirket Profili
                col.Item().Element(c => ComposeCompanyProfile(c));

                // 2. Malzeme Listesi (Tablo)
                col.Item().Element(c => ComposeStandardQuoteTable(c, quote));

                // 3. Finansal Özet
                col.Item().Element(c => ComposeStandardQuoteSummary(c, quote));

                // 4. Ticari Şartlar
                if (!string.IsNullOrWhiteSpace(quote.TermsAndConditions))
                {
                    col.Item().PaddingTop(10).Column(c =>
                    {
                        c.Item().Text("ŞARTLAR VE KOŞULLAR").FontSize(10).Bold().FontColor(BrandColors.Secondary);
                        c.Item().PaddingTop(5).Background("#FDFDFD").Border(1).BorderColor("#E0E0E0").Padding(10)
                         .Text(quote.TermsAndConditions).FontSize(9);
                    });
                }
            });
        }

        private void ComposeStandardQuoteTable(IContainer container, Quote quote)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3); // Ürün Adı
                    cols.RelativeColumn(1); // Miktar
                    cols.RelativeColumn(1); // Birim Fiyat
                    cols.RelativeColumn(1); // İskonto
                    cols.RelativeColumn(1); // Tutar
                });

                table.Header(header =>
                {
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).Text("Ürün / Hizmet").Bold().FontColor(BrandColors.Primary);
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Miktar").Bold().FontColor(BrandColors.Primary);
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Birim Fiyat").Bold().FontColor(BrandColors.Primary);
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("İskonto").Bold().FontColor(BrandColors.Primary);
                    header.Cell().Background(BrandColors.TableHeader).Padding(8).AlignRight().Text("Tutar").Bold().FontColor(BrandColors.Primary);
                });

                foreach (var line in quote.Lines)
                {
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).Text(line.ProductName ?? line.ProductCode).FontSize(9);
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.Quantity:N2}").FontSize(9);
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.UnitPrice:C2}").FontSize(9);
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.DiscountPercent:N2}%").FontSize(9);
                    table.Cell().BorderBottom(1).BorderColor("#EEEEEE").Padding(8).AlignRight().Text($"{line.LineTotal:C2}").FontSize(9);
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
            GenerateStandardQuote(new Quote
            {
                QuoteNumber = $"INV-{order.Id}",
                Date = order.Date,
                ValidUntil = order.Date.AddDays(30),
                Customer = order.Customer,
                TermsAndConditions = order.Notes
            }, filePath);
        }

        public void GeneratePurchaseOrder(PurchaseInvoice invoice, string filePath)
        {
            GenerateStandardQuote(new Quote
            {
                QuoteNumber = $"PO-{invoice.Id}",
                Date = invoice.Date,
                ValidUntil = invoice.Date.AddDays(30),
                TermsAndConditions = invoice.Notes ?? string.Empty
            }, filePath);
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

        #endregion

        #endregion

        #endregion
    }
}
