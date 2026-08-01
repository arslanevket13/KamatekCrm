using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.Services
{
    public class ThermalReceiptPrintService : IThermalReceiptPrintService
    {
        private readonly IPersonalDataProtectionService _personalDataProtection;

        public ThermalReceiptPrintService(IPersonalDataProtectionService personalDataProtection)
        {
            _personalDataProtection = personalDataProtection;
        }

        public Task PrintReceiptAsync(SalesOrder salesOrder, string? printerName = null)
        {
            if (salesOrder == null) throw new ArgumentNullException(nameof(salesOrder));

            return Task.Run(() =>
            {
                try
                {
                    using var pd = new PrintDocument();
                    if (!string.IsNullOrWhiteSpace(printerName) && printerName != "Varsayılan Sistem Yazıcısı")
                    {
                        pd.PrinterSettings.PrinterName = printerName;
                    }

                    pd.PrintPage += (sender, ev) =>
                    {
                        DrawReceiptContent(ev, salesOrder);
                    };

                    pd.Print();
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Fiş yazdırılırken hata oluştu:\n{ex.Message}", "Yazdırma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        public string FormatReceiptText(SalesOrder salesOrder)
        {
            if (salesOrder == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("=============== KAMATEK CRM ===============");
            sb.AppendLine("             PERAKENDE SATIŞ FİŞİ           ");
            sb.AppendLine("===========================================");
            sb.AppendLine($"Fiş No   : {salesOrder.OrderNumber}");
            sb.AppendLine($"Tarih    : {salesOrder.Date.ToLocalTime():dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Müşteri  : {salesOrder.CustomerName}");
            sb.AppendLine($"Ödeme    : {salesOrder.PaymentMethod}");
            sb.AppendLine("-------------------------------------------");
            sb.AppendLine("Ürün Adı             Adet  Fiyat   Tutar   ");
            sb.AppendLine("-------------------------------------------");

            foreach (var item in salesOrder.Items)
            {
                var name = item.ProductName.Length > 20 ? item.ProductName.Substring(0, 17) + "..." : item.ProductName.PadRight(20);
                sb.AppendLine($"{name} {item.Quantity,3} {item.UnitPrice,7:N2} {item.LineTotal,8:N2}");
            }

            sb.AppendLine("-------------------------------------------");
            sb.AppendLine($"Ara Toplam    : {salesOrder.SubTotal,12:N2} ₺");
            sb.AppendLine($"İndirim Toplam: {salesOrder.DiscountTotal,12:N2} ₺");
            sb.AppendLine($"KDV Toplam    : {salesOrder.TaxTotal,12:N2} ₺");
            sb.AppendLine($"GENEL TOPLAM  : {salesOrder.TotalAmount,12:N2} ₺");
            sb.AppendLine("===========================================");
            sb.AppendLine("       Bizi tercih ettiğiniz için           ");
            sb.AppendLine("             Teşekkür Ederiz!              ");
            sb.AppendLine("===========================================");

            return sb.ToString();
        }

        private void DrawReceiptContent(PrintPageEventArgs ev, SalesOrder salesOrder)
        {
            var g = ev.Graphics;
            if (g == null) return;

            using var titleFont = new Font("Courier New", 11, System.Drawing.FontStyle.Bold);
            using var headerFont = new Font("Courier New", 9, System.Drawing.FontStyle.Bold);
            using var bodyFont = new Font("Courier New", 8, System.Drawing.FontStyle.Regular);
            using var boldFont = new Font("Courier New", 8, System.Drawing.FontStyle.Bold);

            float y = 10;
            float leftMargin = 5;
            float width = 270; // 80mm printable width

            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };

            // Header
            g.DrawString("KAMATEK TEKNOLOJİ", titleFont, Brushes.Black, new RectangleF(leftMargin, y, width, 20), centerFormat);
            y += 20;
            g.DrawString("PERAKENDE SATIŞ FİŞİ", headerFont, Brushes.Black, new RectangleF(leftMargin, y, width, 16), centerFormat);
            y += 18;

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            g.DrawString($"Fiş No : {salesOrder.OrderNumber}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            g.DrawString($"Tarih  : {salesOrder.Date.ToLocalTime():dd.MM.yyyy HH:mm}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            g.DrawString($"Müşteri: {salesOrder.CustomerName}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            g.DrawString($"Ödeme  : {salesOrder.PaymentMethod}", bodyFont, Brushes.Black, leftMargin, y); y += 14;

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            // Items Table Header
            g.DrawString("Ürün", boldFont, Brushes.Black, leftMargin, y);
            g.DrawString("Ad.", boldFont, Brushes.Black, leftMargin + 140, y);
            g.DrawString("Fiyat", boldFont, Brushes.Black, leftMargin + 175, y);
            g.DrawString("Tutar", boldFont, Brushes.Black, leftMargin + 225, y);
            y += 14;

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            // Items
            foreach (var item in salesOrder.Items)
            {
                var name = item.ProductName.Length > 22 ? item.ProductName.Substring(0, 20) + ".." : item.ProductName;
                g.DrawString(name, bodyFont, Brushes.Black, leftMargin, y);
                g.DrawString(item.Quantity.ToString(), bodyFont, Brushes.Black, leftMargin + 140, y);
                g.DrawString(item.UnitPrice.ToString("N2"), bodyFont, Brushes.Black, leftMargin + 170, y);
                g.DrawString(item.LineTotal.ToString("N2"), bodyFont, Brushes.Black, leftMargin + 220, y);
                y += 14;
            }

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            // Totals
            g.DrawString("Ara Toplam:", bodyFont, Brushes.Black, leftMargin, y);
            g.DrawString($"{salesOrder.SubTotal:N2} TL", bodyFont, Brushes.Black, leftMargin + 180, y);
            y += 14;

            if (salesOrder.DiscountTotal > 0)
            {
                g.DrawString("İndirim Toplam:", bodyFont, Brushes.Black, leftMargin, y);
                g.DrawString($"-{salesOrder.DiscountTotal:N2} TL", bodyFont, Brushes.Black, leftMargin + 180, y);
                y += 14;
            }

            g.DrawString("KDV Toplam:", bodyFont, Brushes.Black, leftMargin, y);
            g.DrawString($"{salesOrder.TaxTotal:N2} TL", bodyFont, Brushes.Black, leftMargin + 180, y);
            y += 14;

            g.DrawString("GENEL TOPLAM:", titleFont, Brushes.Black, leftMargin, y);
            g.DrawString($"{salesOrder.TotalAmount:N2} TL", titleFont, Brushes.Black, leftMargin + 150, y);
            y += 22;

            g.DrawString("==========================================", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;
            g.DrawString("Teşekkür Ederiz!", headerFont, Brushes.Black, new RectangleF(leftMargin, y, width, 16), centerFormat);
            y += 16;
            g.DrawString("www.kamatek.com.tr", bodyFont, Brushes.Black, new RectangleF(leftMargin, y, width, 14), centerFormat);
        }

        public Task PrintServiceJobTicketAsync(ServiceJob job, string? printerName = null)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            return Task.Run(() =>
            {
                try
                {
                    using var pd = new PrintDocument();
                    if (!string.IsNullOrWhiteSpace(printerName) && printerName != "Varsayılan Sistem Yazıcısı")
                    {
                        pd.PrinterSettings.PrinterName = printerName;
                    }

                    pd.PrintPage += (sender, ev) =>
                    {
                        DrawServiceJobTicketContent(ev, job);
                    };

                    pd.Print();
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Cihaz kabul fişi yazdırılırken hata oluştu:\n{ex.Message}", "Yazdırma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private void DrawServiceJobTicketContent(PrintPageEventArgs ev, ServiceJob job)
        {
            var g = ev.Graphics;
            if (g == null) return;

            using var titleFont = new Font("Courier New", 11, System.Drawing.FontStyle.Bold);
            using var headerFont = new Font("Courier New", 9, System.Drawing.FontStyle.Bold);
            using var bodyFont = new Font("Courier New", 8, System.Drawing.FontStyle.Regular);
            using var boldFont = new Font("Courier New", 8, System.Drawing.FontStyle.Bold);

            float y = 10;
            float leftMargin = 5;
            float width = 270;

            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };

            // Header
            g.DrawString("KAMATEK TEKNOLOJİ", titleFont, Brushes.Black, new RectangleF(leftMargin, y, width, 20), centerFormat);
            y += 20;
            g.DrawString("CİHAZ KABUL & ARIZA FİŞİ", headerFont, Brushes.Black, new RectangleF(leftMargin, y, width, 16), centerFormat);
            y += 18;

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            g.DrawString($"Servis No: #{job.Id}", titleFont, Brushes.Black, leftMargin, y); y += 18;
            g.DrawString($"Tarih    : {job.CreatedDate.ToLocalTime():dd.MM.yyyy HH:mm}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            g.DrawString($"Müşteri  : {job.Customer?.FullName ?? "Belirtilmedi"}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            var protectedPhone = _personalDataProtection.Protect(job.Customer?.PhoneNumber, PersonalDataKind.Phone);
            g.DrawString($"Tel      : {protectedPhone}", bodyFont, Brushes.Black, leftMargin, y); y += 14;

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            g.DrawString("CİHAZ BİLGİLERİ", boldFont, Brushes.Black, leftMargin, y); y += 14;
            g.DrawString($"Marka/Mod: {job.DeviceBrand} {job.DeviceModel}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            if (!string.IsNullOrWhiteSpace(job.SerialNumber))
            {
                g.DrawString($"Seri No  : {job.SerialNumber}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            }
            if (!string.IsNullOrWhiteSpace(job.Accessories))
            {
                g.DrawString($"Aksesuar : {job.Accessories}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
            }

            g.DrawString("------------------------------------------", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;

            g.DrawString("ARIZA ŞİKAYETİ / DURUM", boldFont, Brushes.Black, leftMargin, y); y += 14;
            var desc = job.Description ?? "";
            if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";
            g.DrawString(desc, bodyFont, Brushes.Black, new RectangleF(leftMargin, y, width, 40));
            y += 36;

            if (job.LaborCost > 0)
            {
                g.DrawString($"Ön Tahmini Ücret: {job.LaborCost:N2} TL", boldFont, Brushes.Black, leftMargin, y);
                y += 16;
            }

            g.DrawString("==========================================", bodyFont, Brushes.Black, leftMargin, y);
            y += 12;
            g.DrawString("Cihazınız teslim alınmıştır.", bodyFont, Brushes.Black, new RectangleF(leftMargin, y, width, 14), centerFormat);
            y += 14;
            g.DrawString("www.kamatek.com.tr", bodyFont, Brushes.Black, new RectangleF(leftMargin, y, width, 14), centerFormat);
        }
    }
}
