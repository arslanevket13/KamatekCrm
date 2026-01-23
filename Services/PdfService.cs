using System;
using System.Globalization;
using System.Linq;
using KamatekCrm.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KamatekCrm.Services
{
    /// <summary>
    /// PDF Oluşturma Servisi
    /// </summary>
    public class PdfService
    {
        private static readonly CultureInfo TurkishCulture = new("tr-TR");

        /// <summary>
        /// Servis Formu PDF'i oluştur
        /// </summary>
        /// <param name="job">Servis işi</param>
        /// <param name="filePath">Dosya yolu</param>
        public void GenerateServiceForm(ServiceJob job, string filePath)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                    page.Header().Element(c => ComposeHeader(c, job));
                    page.Content().Element(c => ComposeContent(c, job));
                    page.Footer().Element(c => ComposeFooter(c, job));
                });
            });

            document.GeneratePdf(filePath);
        }

        /// <summary>
        /// Header bölümü
        /// </summary>
        private void ComposeHeader(IContainer container, ServiceJob job)
        {
            container.Column(column =>
            {
                column.Spacing(10);

                // Üst Başlık
                column.Item().Row(row =>
                {
                    // Logo/Şirket Adı
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("KAMATEK")
                            .Bold().FontSize(24).FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Teknik Servis & Güvenlik Sistemleri")
                            .FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().Text("Tel: 0850 XXX XX XX | info@kamatek.com.tr")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    // Servis Formu Başlığı
                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Background(Colors.Blue.Darken2).Padding(10).Column(inner =>
                        {
                            inner.Item().Text("SERVİS FORMU").Bold().FontSize(16).FontColor(Colors.White);
                            inner.Item().Text($"#{job.Id:D6}").FontSize(14).FontColor(Colors.White);
                        });
                        col.Item().AlignRight().Text($"Tarih: {job.CreatedDate:dd.MM.yyyy}")
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                });

                // Ayırıcı çizgi
                column.Item().LineHorizontal(2).LineColor(Colors.Blue.Darken2);
            });
        }

        /// <summary>
        /// Content bölümü
        /// </summary>
        private void ComposeContent(IContainer container, ServiceJob job)
        {
            container.PaddingVertical(15).Column(column =>
            {
                column.Spacing(15);

                // Müşteri ve İş Bilgileri
                column.Item().Row(row =>
                {
                    // Müşteri Bilgileri
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().Text("MÜŞTERİ BİLGİLERİ").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Ad Soyad: ").SemiBold();
                            text.Span(job.Customer?.FullName ?? "—");
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Telefon: ").SemiBold();
                            text.Span(job.Customer?.PhoneNumber ?? "—");
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Adres: ").SemiBold();
                            text.Span(job.Customer?.FullAddress ?? "—");
                        });
                    });

                    row.ConstantItem(15); // Boşluk

                    // İş Bilgileri
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().Text("İŞ BİLGİLERİ").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Kategori: ").SemiBold();
                            text.Span(job.JobCategory.ToString());
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Durum: ").SemiBold();
                            text.Span(GetStatusText(job.Status));
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Öncelik: ").SemiBold();
                            text.Span(GetPriorityText(job.Priority));
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Teknisyen: ").SemiBold();
                            text.Span(job.AssignedTechnician ?? "Atanmadı");
                        });
                    });
                });

                // İş Açıklaması
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                {
                    col.Item().Text("İŞ AÇIKLAMASI").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(8).Text(job.Description ?? "Açıklama girilmemiş.")
                        .FontSize(10);
                });

                // Kullanılan Malzemeler
                if (job.ServiceJobItems != null && job.ServiceJobItems.Any())
                {
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().Text("KULLANILAN MALZEMELER").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                        col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            // Tablo başlıkları
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // #
                                columns.RelativeColumn(3);   // Ürün Adı
                                columns.RelativeColumn(1);   // Miktar
                                columns.RelativeColumn(1);   // Birim
                                columns.RelativeColumn(1);   // Birim Fiyat
                                columns.RelativeColumn(1);   // Toplam
                            });

                            // Header satırı
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Ürün Adı").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text("Miktar").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text("Birim").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("B. Fiyat").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Toplam").SemiBold();
                            });

                            int index = 1;
                            foreach (var item in job.ServiceJobItems)
                            {
                                var itemTotal = item.UnitPrice * item.QuantityUsed;
                                table.Cell().Padding(5).Text(index.ToString());
                                table.Cell().Padding(5).Text(item.Product?.ProductName ?? "—");
                                table.Cell().Padding(5).AlignCenter().Text(item.QuantityUsed.ToString());
                                table.Cell().Padding(5).AlignCenter().Text(item.Product?.Unit ?? "Adet");
                                table.Cell().Padding(5).AlignRight().Text($"{item.UnitPrice:N2} ₺");
                                table.Cell().Padding(5).AlignRight().Text($"{itemTotal:N2} ₺");
                                index++;
                            }
                        });
                    });
                }

                // Maliyet Özeti
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                {
                    col.Item().Text("MALİYET ÖZETİ").Bold().FontSize(11).FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem(); // Boşluk

                        row.ConstantItem(200).Column(costCol =>
                        {
                            var materialCost = job.ServiceJobItems?.Sum(x => x.UnitPrice * x.QuantityUsed) ?? 0;

                            costCol.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Malzeme Toplamı:").SemiBold();
                                r.ConstantItem(80).AlignRight().Text($"{materialCost:N2} ₺");
                            });
                            costCol.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("İşçilik:").SemiBold();
                                r.ConstantItem(80).AlignRight().Text($"{job.LaborCost:N2} ₺");
                            });
                            costCol.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("İndirim:").SemiBold();
                                r.ConstantItem(80).AlignRight().Text($"-{job.DiscountAmount:N2} ₺").FontColor(Colors.Red.Medium);
                            });
                            costCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                            costCol.Item().PaddingTop(5).Row(r =>
                            {
                                r.RelativeItem().Text("GENEL TOPLAM:").Bold().FontSize(12);
                                r.ConstantItem(80).AlignRight().Text($"{job.TotalAmount:N2} ₺").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            });
                        });
                    });
                });

                // İmza Alanları
                column.Item().PaddingTop(20).Row(row =>
                {
                    // Teknisyen İmza
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(15).Column(col =>
                    {
                        col.Item().Text("TEKNİSYEN İMZA").Bold().FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(5).Text(job.AssignedTechnician ?? "Ad Soyad")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(20); // Boşluk

                    // Müşteri İmza
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(15).Column(col =>
                    {
                        col.Item().Text("MÜŞTERİ İMZA").Bold().FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(40).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(5).Text(job.Customer?.FullName ?? "Ad Soyad")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            });
        }

        /// <summary>
        /// Footer bölümü
        /// </summary>
        private void ComposeFooter(IContainer container, ServiceJob job)
        {
            container.Column(column =>
            {
                column.Spacing(5);

                // Ayırıcı çizgi
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // Garanti / Sorumluluk Notu
                column.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(10).Text(
                    "⚠️ UYARI: Bu servis formu, yapılan işlerin ve kullanılan malzemelerin kayıt altına alınması amacıyla düzenlenmiştir. " +
                    "Garanti kapsamı dışındaki durumlar (kullanıcı hatası, fiziksel hasar, doğal afet vb.) için firmamız sorumluluk kabul etmez. " +
                    "İşbu formu imzalamakla, yukarıda belirtilen işlerin eksiksiz tamamlandığını ve malzemelerin teslim alındığını kabul etmiş sayılırsınız."
                ).FontSize(8).FontColor(Colors.Grey.Darken1).Italic();

                // Sayfa numarası
                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("Sayfa ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        }

        #region Helper Methods

        private static string GetStatusText(Enums.JobStatus status)
        {
            return status switch
            {
                Enums.JobStatus.Pending => "Beklemede",
                Enums.JobStatus.InProgress => "Devam Ediyor",
                Enums.JobStatus.Completed => "Tamamlandı",
                _ => status.ToString()
            };
        }

        private static string GetPriorityText(Enums.JobPriority priority)
        {
            return priority switch
            {
                Enums.JobPriority.Low => "Düşük",
                Enums.JobPriority.Normal => "Normal",
                Enums.JobPriority.Urgent => "Acil",
                Enums.JobPriority.Critical => "Kritik",
                _ => priority.ToString()
            };
        }

        #endregion
    }
}
