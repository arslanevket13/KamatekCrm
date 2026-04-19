using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;

namespace KamatekCrm.API.Services
{
    /// <summary>
    /// PDF Rapor Motoru — QuestPDF ile profesyonel çıktılar.
    /// </summary>
    public interface IPdfReportService
    {
        byte[] GenerateServiceJobReport(ServiceJobPdfData data);
        byte[] GenerateInvoice(InvoicePdfData data);
        byte[] GenerateMonthlySummary(MonthlySummaryPdfData data);
    }

    public class PdfReportService : IPdfReportService
    {
        // Marka renkleri
        private static readonly string PrimaryColor = "#1E40AF";
        private static readonly string SecondaryColor = "#64748B";
        private static readonly string AccentColor = "#059669";
        private static readonly string DangerColor = "#DC2626";

        public PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>Servis İş Raporu — detaylı</summary>
        public byte[] GenerateServiceJobReport(ServiceJobPdfData data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => CompanyHeader(c, "SERVİS İŞ RAPORU"));

                    page.Content().Column(col =>
                    {
                        // İş Bilgileri
                        col.Item().PaddingVertical(10).Table(table =>
                        {
                            table.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(2);
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(2);
                            });

                            TableInfoRow(table, "İş No:", $"#{data.JobId}");
                            TableInfoRow(table, "Durum:", data.Status);
                            TableInfoRow(table, "Başlık:", data.Title);
                            TableInfoRow(table, "Öncelik:", data.Priority);
                            TableInfoRow(table, "Oluşturma:", data.CreatedDate.ToString("dd.MM.yyyy HH:mm"));
                            TableInfoRow(table, "Teslim:", data.CompletedDate?.ToString("dd.MM.yyyy HH:mm") ?? "—");
                        });

                        // Müşteri Bilgileri
                        col.Item().PaddingVertical(5).Element(c => SectionTitle(c, "MÜŞTERİ BİLGİLERİ"));
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(3);
                            });

                            TableInfoRow(table, "Ad Soyad:", data.CustomerName);
                            TableInfoRow(table, "Telefon:", data.CustomerPhone);
                            TableInfoRow(table, "Adres:", data.CustomerAddress);
                        });

                        // Açıklama
                        col.Item().PaddingVertical(5).Element(c => SectionTitle(c, "AÇIKLAMA"));
                        col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(data.Description);

                        // Kullanılan Parçalar
                        if (data.Parts.Any())
                        {
                            col.Item().PaddingVertical(5).Element(c => SectionTitle(c, "KULLANILAN PARÇALAR"));
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cd =>
                                {
                                    cd.ConstantColumn(30);
                                    cd.RelativeColumn(3);
                                    cd.ConstantColumn(50);
                                    cd.ConstantColumn(80);
                                    cd.ConstantColumn(80);
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Parça").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Adet").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Birim Fiyat").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Toplam").FontColor(Colors.White).Bold();
                                });

                                int i = 1;
                                foreach (var part in data.Parts)
                                {
                                    var bgColor = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bgColor).Padding(4).Text(i.ToString());
                                    table.Cell().Background(bgColor).Padding(4).Text(part.Name);
                                    table.Cell().Background(bgColor).Padding(4).Text(part.Quantity.ToString());
                                    table.Cell().Background(bgColor).Padding(4).Text($"{part.UnitPrice:N2} ₺");
                                    table.Cell().Background(bgColor).Padding(4).Text($"{part.Total:N2} ₺");
                                    i++;
                                }
                            });
                        }

                        // Fiyat Özeti
                        col.Item().PaddingTop(10).AlignRight().Column(priceCol =>
                        {
                            priceCol.Item().Text($"Parça Toplamı: {data.PartsTotal:N2} ₺").FontSize(10);
                            priceCol.Item().Text($"İşçilik: {data.LaborCost:N2} ₺").FontSize(10);
                            if (data.Discount > 0)
                                priceCol.Item().Text($"İndirim: -{data.Discount:N2} ₺").FontSize(10).FontColor(DangerColor);
                            priceCol.Item().PaddingTop(4).Text($"TOPLAM: {data.GrandTotal:N2} ₺")
                                .FontSize(14).Bold().FontColor(PrimaryColor);
                        });

                        // Teknisyen Notu
                        if (!string.IsNullOrEmpty(data.TechnicianNote))
                        {
                            col.Item().PaddingVertical(5).Element(c => SectionTitle(c, "TEKNİSYEN NOTU"));
                            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(data.TechnicianNote);
                        }
                    });

                    page.Footer().Element(Footer);
                });
            });

            var bytes = document.GeneratePdf();
            Log.Information("PDF generated: ServiceJob #{JobId}, {Size} bytes", data.JobId, bytes.Length);
            return bytes;
        }

        /// <summary>Fatura PDF</summary>
        public byte[] GenerateInvoice(InvoicePdfData data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => CompanyHeader(c, "FATURA"));

                    page.Content().Column(col =>
                    {
                        // Fatura Bilgileri
                        col.Item().PaddingVertical(10).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text("Müşteri:").Bold();
                                left.Item().Text(data.CustomerName);
                                left.Item().Text(data.CustomerAddress);
                                left.Item().Text($"Tel: {data.CustomerPhone}");
                                if (!string.IsNullOrEmpty(data.TaxNumber))
                                    left.Item().Text($"VKN: {data.TaxNumber}");
                            });

                            row.RelativeItem().AlignRight().Column(right =>
                            {
                                right.Item().Text($"Fatura No: {data.InvoiceNumber}").Bold();
                                right.Item().Text($"Tarih: {data.Date:dd.MM.yyyy}");
                                right.Item().Text($"Vade: {data.DueDate:dd.MM.yyyy}");
                            });
                        });

                        // Kalemler
                        col.Item().PaddingVertical(10).Table(table =>
                        {
                            table.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(30);
                                cd.RelativeColumn(3);
                                cd.ConstantColumn(50);
                                cd.ConstantColumn(70);
                                cd.ConstantColumn(50);
                                cd.ConstantColumn(70);
                                cd.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(PrimaryColor).Padding(4).Text("#").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("Ürün/Hizmet").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("Miktar").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("B. Fiyat").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("KDV%").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("KDV").FontColor(Colors.White).Bold();
                                header.Cell().Background(PrimaryColor).Padding(4).Text("Toplam").FontColor(Colors.White).Bold();
                            });

                            int i = 1;
                            foreach (var line in data.Lines)
                            {
                                var bgColor = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Background(bgColor).Padding(4).Text(i.ToString());
                                table.Cell().Background(bgColor).Padding(4).Text(line.Description);
                                table.Cell().Background(bgColor).Padding(4).Text(line.Quantity.ToString());
                                table.Cell().Background(bgColor).Padding(4).Text($"{line.UnitPrice:N2}");
                                table.Cell().Background(bgColor).Padding(4).Text($"%{line.VatRate}");
                                table.Cell().Background(bgColor).Padding(4).Text($"{line.VatAmount:N2}");
                                table.Cell().Background(bgColor).Padding(4).Text($"{line.LineTotal:N2} ₺");
                                i++;
                            }
                        });

                        // Toplamlar
                        col.Item().PaddingTop(5).AlignRight().Column(totals =>
                        {
                            totals.Item().Text($"Ara Toplam: {data.SubTotal:N2} ₺");
                            totals.Item().Text($"KDV Toplam: {data.VatTotal:N2} ₺");
                            if (data.DiscountTotal > 0)
                                totals.Item().Text($"İndirim: -{data.DiscountTotal:N2} ₺").FontColor(DangerColor);
                            totals.Item().PaddingTop(4).Text($"GENEL TOPLAM: {data.GrandTotal:N2} ₺")
                                .FontSize(14).Bold().FontColor(PrimaryColor);
                        });

                        // Notlar
                        if (!string.IsNullOrEmpty(data.Notes))
                        {
                            col.Item().PaddingTop(15).Text("Notlar:").Bold();
                            col.Item().Text(data.Notes);
                        }
                    });

                    page.Footer().Element(Footer);
                });
            });

            var bytes = document.GeneratePdf();
            Log.Information("PDF Invoice generated: {InvoiceNo}, {Size} bytes", data.InvoiceNumber, bytes.Length);
            return bytes;
        }

        /// <summary>Aylık Özet Rapor</summary>
        public byte[] GenerateMonthlySummary(MonthlySummaryPdfData data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => CompanyHeader(c, $"AYLIK ÖZET — {data.MonthName} {data.Year}"));

                    page.Content().Column(col =>
                    {
                        // KPI Kartları
                        col.Item().PaddingVertical(10).Row(row =>
                        {
                            KpiCard(row.RelativeItem(), "Toplam Gelir", $"{data.TotalRevenue:N2} ₺", AccentColor);
                            row.ConstantItem(10);
                            KpiCard(row.RelativeItem(), "Toplam Gider", $"{data.TotalExpenses:N2} ₺", DangerColor);
                            row.ConstantItem(10);
                            KpiCard(row.RelativeItem(), "Net Kâr", $"{data.NetProfit:N2} ₺",
                                data.NetProfit >= 0 ? AccentColor : DangerColor);
                        });

                        col.Item().PaddingVertical(5).Row(row =>
                        {
                            KpiCard(row.RelativeItem(), "Servis İşi", data.TotalJobs.ToString(), PrimaryColor);
                            row.ConstantItem(10);
                            KpiCard(row.RelativeItem(), "Tamamlanan", data.CompletedJobs.ToString(), AccentColor);
                            row.ConstantItem(10);
                            KpiCard(row.RelativeItem(), "Yeni Müşteri", data.NewCustomers.ToString(), PrimaryColor);
                        });

                        // En Çok Satan Ürünler
                        if (data.TopProducts.Any())
                        {
                            col.Item().PaddingVertical(5).Element(c => SectionTitle(c, "EN ÇOK SATAN ÜRÜNLER"));
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cd =>
                                {
                                    cd.ConstantColumn(30);
                                    cd.RelativeColumn(3);
                                    cd.ConstantColumn(60);
                                    cd.ConstantColumn(100);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("#").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Ürün").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Adet").FontColor(Colors.White).Bold();
                                    header.Cell().Background(PrimaryColor).Padding(4).Text("Toplam").FontColor(Colors.White).Bold();
                                });

                                int i = 1;
                                foreach (var product in data.TopProducts)
                                {
                                    var bgColor = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                    table.Cell().Background(bgColor).Padding(4).Text(i.ToString());
                                    table.Cell().Background(bgColor).Padding(4).Text(product.Name);
                                    table.Cell().Background(bgColor).Padding(4).Text(product.Quantity.ToString());
                                    table.Cell().Background(bgColor).Padding(4).Text($"{product.Revenue:N2} ₺");
                                    i++;
                                }
                            });
                        }
                    });

                    page.Footer().Element(Footer);
                });
            });

            var bytes = document.GeneratePdf();
            Log.Information("PDF Monthly Summary generated: {Month}/{Year}, {Size} bytes", data.MonthName, data.Year, bytes.Length);
            return bytes;
        }

        #region Shared Components

        private void CompanyHeader(IContainer container, string title)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("KAMATEK CRM").FontSize(18).Bold().FontColor(PrimaryColor);
                    col.Item().Text("Teknik Servis & CRM Yönetim Sistemi").FontSize(8).FontColor(SecondaryColor);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(title).FontSize(14).Bold().FontColor(PrimaryColor);
                    col.Item().Text($"Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(SecondaryColor);
                });
            });
        }

        private void SectionTitle(IContainer container, string title)
        {
            container.PaddingBottom(4).BorderBottom(1).BorderColor(PrimaryColor)
                .Text(title).FontSize(11).Bold().FontColor(PrimaryColor);
        }

        private void TableInfoRow(TableDescriptor table, string label, string value)
        {
            table.Cell().Padding(3).Text(label).Bold().FontColor(SecondaryColor);
            table.Cell().Padding(3).Text(value);
        }

        private void KpiCard(IContainer container, string title, string value, string color)
        {
            container.Border(1).BorderColor(color).Padding(10).Column(col =>
            {
                col.Item().Text(title).FontSize(8).FontColor(SecondaryColor);
                col.Item().Text(value).FontSize(16).Bold().FontColor(color);
            });
        }

        private void Footer(IContainer container)
        {
            container.AlignCenter().Text(t =>
            {
                t.Span("KamatekCRM © 2026 — ").FontSize(8).FontColor(SecondaryColor);
                t.CurrentPageNumber().FontSize(8);
                t.Span(" / ").FontSize(8);
                t.TotalPages().FontSize(8);
            });
        }

        #endregion
    }

    #region PDF DTOs

    public class ServiceJobPdfData
    {
        public int JobId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string CustomerAddress { get; set; } = "";
        public string? TechnicianNote { get; set; }
        public List<PartLineItem> Parts { get; set; } = new();
        public decimal PartsTotal { get; set; }
        public decimal LaborCost { get; set; }
        public decimal Discount { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class PartLineItem
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
    }

    public class InvoicePdfData
    {
        public string InvoiceNumber { get; set; } = "";
        public DateTime Date { get; set; }
        public DateTime DueDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerAddress { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string? TaxNumber { get; set; }
        public List<InvoiceLine> Lines { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string? Notes { get; set; }
    }

    public class InvoiceLine
    {
        public string Description { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class MonthlySummaryPdfData
    {
        public int Year { get; set; }
        public string MonthName { get; set; } = "";
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public int TotalJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int NewCustomers { get; set; }
        public List<ProductSummaryItem> TopProducts { get; set; } = new();
    }

    public class ProductSummaryItem
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    #endregion
}
