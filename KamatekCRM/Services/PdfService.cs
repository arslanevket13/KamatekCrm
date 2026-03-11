using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KamatekCrm.Services
{
    public class PdfService
    {
        static PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
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
                    c.Item().Text(project.Customer?.FullAddress ?? "Adres bilgisi girilmemiş").FontSize(9);
                    c.Item().Text($"Tel: {project.Customer?.PhoneNumber ?? "-"}").FontSize(9);
                    if (!string.IsNullOrEmpty(project.Customer?.Email))
                        c.Item().Text($"E-posta: {project.Customer.Email}").FontSize(9);
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

        #endregion
    }
}
