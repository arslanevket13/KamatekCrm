using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KamatekCrm.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KamatekCrm.Services
{
    public class PdfService
    {
        // Renk Paleti (Kurumsal)
        private static class BrandColors
        {
            public static string Primary = "#1A237E";   // Lacivert (Navy Blue)
            public static string Secondary = "#3949AB"; // Açık Lacivert
            public static string Accent = "#F57C00";    // Turuncu
            public static string TextPrimary = "#212121";
            public static string TextSecondary = "#757575";
            public static string LightGray = "#F5F5F5";
            public static string TableHeader = "#E8EAF6";
        }

        public void GenerateProjectQuote(ServiceProject project, List<ScopeNode> rootNodes, string filePath)
        {
            // Logo kontrolü
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "KamatekLogo.jpg");
            byte[]? logoBytes = null;
            if (File.Exists(logoPath))
            {
                logoBytes = File.ReadAllBytes(logoPath);
            }

            // Hiyerarşik veriyi düz listeye çevir (Tablo çizimi için)
            var flattenedItems = FlattenScopeNodes(rootNodes);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.SegoeUI));

                    page.Header().Element(c => ComposeHeader(c, project, logoBytes));
                    page.Content().Element(c => ComposeContent(c, project, flattenedItems));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            })
            .GeneratePdf(filePath);
        }

        #region Composition Methods

        private void ComposeHeader(IContainer container, ServiceProject project, byte[]? logoBytes)
        {
            container.Row(row =>
            {
                // Sol: Logo
                if (logoBytes != null)
                {
                    row.ConstantItem(150).Image(logoBytes).FitArea();
                }
                else
                {
                    row.ConstantItem(150).Text("KAMATEK").FontSize(24).Bold().FontColor(BrandColors.Primary);
                }

                // Sağ: Teklif Başlığı ve Tarih
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignRight().Text("TEKNİK VE TİCARİ TEKLİF").FontSize(16).Bold().FontColor(BrandColors.Primary);
                    col.Item().AlignRight().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontColor(BrandColors.TextSecondary);
                    col.Item().AlignRight().Text($"Teklif No: {project.ProjectCode}").FontColor(BrandColors.TextSecondary);
                });
            });
        }

        private void ComposeContent(IContainer container, ServiceProject project, List<PdfLineItem> items)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(20);

                // 1. Müşteri Bilgileri
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("MÜŞTERİ BİLGİLERİ").FontSize(9).Bold().FontColor(BrandColors.Secondary);
                        c.Item().Text(project.Customer?.FullName ?? "Sayın Müşteri").Bold();
                        c.Item().Text(project.Customer?.FullAddress ?? "Adres bilgisi girilmemiş.");
                        c.Item().Text($"Tel: {project.Customer?.PhoneNumber}");
                    });
                });

                // 2. Yönetici Özeti (Dinamik Metin)
                column.Item().Column(c =>
                {
                    c.Item().Text("PROJE KAPSAMI VE ÖZET").FontSize(12).Bold().FontColor(BrandColors.Primary);
                    c.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span("Bu teklif, ");
                        text.Span(project.Title).Bold();
                        text.Span(" projesi kapsamında yapılacak olan teknik sistemlerin malzeme temini, montajı ve devreye alınmasını kapsamaktadır. Sistem tasarımı, en güncel teknolojiler kullanılarak uzun ömürlü ve performanslı çalışma prensibi gözetilerek hazırlanmıştır.");
                    });
                });

                // 3. Malzeme ve Hizmet Listesi Tablosu
                column.Item().Element(c => ComposeTable(c, items));

                // 4. Finansal Özet
                column.Item().Element(c => ComposeFinancialSummary(c, items));

                // 5. Ticari Şartlar
                column.Item().Element(ComposeCommercialTerms);

                // 6. İmza Bloğu
                column.Item().Element(ComposeSignatures);
            });
        }

        private void ComposeTable(IContainer container, List<PdfLineItem> items)
        {
            container.Table(table =>
            {
                // Sütun Tanımları
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // Sıra No
                    columns.RelativeColumn(4);  // Ürün/Açıklama
                    columns.RelativeColumn(1);  // Birim
                    columns.RelativeColumn(1);  // Miktar
                    columns.RelativeColumn(1.5f); // B.Fiyat
                    columns.RelativeColumn(1.5f); // Toplam
                });

                // Başlık
                table.Header(header =>
                {
                    header.Cell().RowSpan(1).ColumnSpan(6).PaddingBottom(5).Text("MALZEME VE HİZMET LİSTESİ").FontSize(12).Bold().FontColor(BrandColors.Primary);
                    
                    static IContainer HeaderStyle(IContainer c) => c.Background(BrandColors.TableHeader).Padding(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

                    header.Cell().Element(HeaderStyle).Text("#").Bold();
                    header.Cell().Element(HeaderStyle).Text("Açıklama / Ürün").Bold();
                    header.Cell().Element(HeaderStyle).Text("Birim").Bold();
                    header.Cell().Element(HeaderStyle).Text("Miktar").AlignRight().Bold();
                    header.Cell().Element(HeaderStyle).Text("B.Fiyat").AlignRight().Bold();
                    header.Cell().Element(HeaderStyle).Text("Toplam").AlignRight().Bold();
                });

                // Satırlar
                foreach (var item in items)
                {
                    // Section Header (Blok, Kat vb.) - Koyu Gri Arkaplan
                    if (item.IsSectionHeader)
                    {
                        table.Cell().ColumnSpan(6).Background(BrandColors.LightGray).Padding(5).PaddingLeft(5 + (item.Level * 10)).Text(item.Name).Bold().FontColor(BrandColors.Primary);
                    }
                    else
                    {
                        // Normal Ürün Satırı
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text((items.IndexOf(item) + 1).ToString()).FontSize(8).FontColor(Colors.Grey.Medium);
                        
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).PaddingLeft(5 + (item.Level * 10)).Text(item.Name);
                        
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text("Adet"); // Veya item.Unit
                        
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(item.Quantity.ToString());
                        
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"{item.UnitPrice:N2} ₺");
                        
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"{item.TotalPrice:N2} ₺").Bold();
                    }
                }
            });
        }

        private void ComposeFinancialSummary(IContainer container, List<PdfLineItem> items)
        {
            var subTotal = items.Where(i => !i.IsSectionHeader).Sum(i => i.TotalPrice);
            var vatTotal = subTotal * 0.20m;
            var grandTotal = subTotal + vatTotal;

            container.PaddingTop(10).Row(row =>
            {
                row.RelativeItem(); // Sol taraf boş
                row.RelativeItem().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Ara Toplam:").AlignRight();
                        r.RelativeItem().Text($"{subTotal:N2} ₺").AlignRight();
                    });
                    
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("KDV (%20):").AlignRight();
                        r.RelativeItem().Text($"{vatTotal:N2} ₺").AlignRight();
                    });

                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Black);

                    col.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("GENEL TOPLAM:").Bold().FontSize(12).AlignRight();
                        r.RelativeItem().Text($"{grandTotal:N2} ₺").Bold().FontSize(12).FontColor(BrandColors.Primary).AlignRight();
                    });
                });
            });
        }

        private void ComposeCommercialTerms(IContainer container)
        {
            container.Background(BrandColors.LightGray).Padding(10).Column(col =>
            {
                col.Item().Text("TİCARİ ŞARTLAR").Bold().Underline();
                col.Item().Text("1. Teklifimiz 15 gün süreyle geçerlidir.");
                col.Item().Text("2. Ürünlerimiz malzeme ve işçilik hatalarına karşı 2 yıl garantilidir.");
                col.Item().Text("3. Ödeme Planı: %50 Sipariş onayı ile peşin, %50 İş tesliminde nakit/havale.");
                col.Item().Text("4. Fiyatlara KDV dahildir (yukarıda belirtilmiştir).");
            });
        }

        private void ComposeSignatures(IContainer container)
        {
            container.PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Hazırlayan").Bold().AlignCenter();
                    col.Item().Text("KAMATEK ELEKTRONİK").AlignCenter();
                    col.Item().PaddingTop(30).LineHorizontal(1).LineColor(Colors.Black);
                });

                row.ConstantItem(50); // Boşluk

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Onaylayan").Bold().AlignCenter();
                    col.Item().Text("MÜŞTERİ / YETKİLİ").AlignCenter();
                    col.Item().PaddingTop(30).LineHorizontal(1).LineColor(Colors.Black);
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text("KamatekCRM tarafından oluşturulmuştur.").FontSize(8).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(x => 
                {
                    x.Span("Sayfa ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

        #endregion

        #region Helper: Flatten Recursive Structure

        // Recursive ağacı düz listeye çevirir ve indentasyon seviyesini ekler
        private List<PdfLineItem> FlattenScopeNodes(List<ScopeNode> nodes, int level = 0)
        {
            var list = new List<PdfLineItem>();

            foreach (var node in nodes)
            {
                // Node'un kendisi (Başlık olarak) - Sadece kalemi varsa veya çocukları varsa ekle
                if (node.Items.Any() || node.Children.Any())
                {
                    list.Add(new PdfLineItem
                    {
                        Name = node.Name,
                        IsSectionHeader = true,
                        Level = level
                    });
                }

                // Node'un kalemleri (Ürünler)
                foreach (var item in node.Items)
                {
                    list.Add(new PdfLineItem
                    {
                        Name = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice,
                        IsSectionHeader = false,
                        Level = level + 1
                    });
                }

                // Alt node'lar (Recursive)
                if (node.Children.Any())
                {
                    list.AddRange(FlattenScopeNodes(node.Children.ToList(), level + 1));
                }
            }

            return list;
        }

        // PDF Tablosu için yardımcı model
        private class PdfLineItem
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal TotalPrice { get; set; }
            public bool IsSectionHeader { get; set; }
            public int Level { get; set; }
        }

        #endregion

        public void GenerateServiceForm(ServiceJob job, string filePath)
        {
             Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    
                    page.Header().Text("SERVİS FORMU").AlignCenter().FontSize(20).Bold();
                    
                    page.Content().PaddingVertical(20).Column(col => 
                    {
                        col.Item().Text($"İş Emri No: {job.Id}").FontSize(14).Bold();
                        col.Item().Text($"Müşteri: {job.Customer?.FullName ?? "-"}");
                        col.Item().Text($"Tarih: {job.CreatedDate:dd.MM.yyyy}");
                        col.Item().PaddingTop(20).Text("Detaylar:");
                        col.Item().Text(job.Description ?? "Açıklama yok.");
                    });
                });
            })
            .GeneratePdf(filePath);
        }
    }
}
