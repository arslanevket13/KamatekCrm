using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.Services
{
    public class ThermalReceiptPrintService : IThermalReceiptPrintService
    {
        private readonly IPersonalDataProtectionService _personalDataProtection;
        private readonly IAuditTrailService _auditTrail;

        public ThermalReceiptPrintService(
            IPersonalDataProtectionService personalDataProtection,
            IAuditTrailService auditTrail)
        {
            _personalDataProtection = personalDataProtection;
            _auditTrail = auditTrail;
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

        public byte[] GetReceiptBytes(SalesOrder salesOrder, Encoding? encoding = null)
        {
            var text = FormatReceiptText(salesOrder);
            if (encoding != null) return encoding.GetBytes(text);

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding("windows-1254").GetBytes(text);
            }
            catch
            {
                return Encoding.UTF8.GetBytes(text);
            }
        }

        public Task PrintSalesReturnReceiptAsync(SalesReturnReceiptDto salesReturn, string? printerName = null)
        {
            ArgumentNullException.ThrowIfNull(salesReturn);
            return Task.Run(() =>
            {
                using var document = new PrintDocument();
                if (!string.IsNullOrWhiteSpace(printerName) && printerName != "Varsayılan Sistem Yazıcısı")
                    document.PrinterSettings.PrinterName = printerName;
                document.PrintPage += (_, args) =>
                {
                    using var title = new Font("Courier New", 11, System.Drawing.FontStyle.Bold);
                    using var body = new Font("Courier New", 8);
                    var graphics = args.Graphics;
                    if (graphics is null) return;
                    float y = 10;
                    void Line(string text, Font? font = null)
                    {
                        graphics.DrawString(text, font ?? body, Brushes.Black, 5, y);
                        y += 14;
                    }
                    Line("KAMATEK SATIŞ İADE FİŞİ", title);
                    Line("------------------------------------------");
                    Line($"İade No : {salesReturn.ReturnNumber}");
                    Line($"Satış No: {salesReturn.SalesOrderNumber}");
                    Line($"Tarih   : {salesReturn.Date.ToLocalTime():dd.MM.yyyy HH:mm}");
                    Line($"Neden   : {salesReturn.Reason}");
                    Line("------------------------------------------");
                    foreach (var item in salesReturn.Lines)
                        Line($"{item.ProductName}  {item.Quantity} ad.  {item.LineTotal:N2} TL");
                    Line("------------------------------------------");
                    Line($"İADE TOPLAMI: {salesReturn.TotalAmount:N2} TL", title);
                    foreach (var payment in salesReturn.Payments)
                        Line($"{payment.PaymentMethod}: {payment.Amount:N2} TL");
                    Line("------------------------------------------");
                    Line("Bu belge finansal iade kaydıdır.");
                };
                document.Print();
                var audit = _auditTrail.WriteAsync(AuditActionType.View, "SalesReturnReceipt", salesReturn.SalesReturnId.ToString(), $"İade fişi yazdırıldı: {salesReturn.ReturnNumber}").GetAwaiter().GetResult();
                if (audit.IsFailure)
                    System.Diagnostics.Debug.WriteLine(audit.Error);
            });
        }

        private void DrawReceiptContent(PrintPageEventArgs ev, SalesOrder salesOrder)
        {
            var g = ev.Graphics;
            if (g == null) return;

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

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
            
            var customerName = salesOrder.CustomerName ?? "Perakende";
            if (customerName.Length > 24) customerName = customerName.Substring(0, 22) + "..";
            g.DrawString($"Müşteri: {customerName}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
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

            // Items (Word-wrap & Truncation Safe)
            foreach (var item in salesOrder.Items)
            {
                var productWidth = 135f;
                var name = item.ProductName ?? string.Empty;
                var nameSize = g.MeasureString(name, bodyFont, (int)productWidth);
                var rowHeight = Math.Max(14f, nameSize.Height);

                g.DrawString(name, bodyFont, Brushes.Black, new RectangleF(leftMargin, y, productWidth, rowHeight));
                g.DrawString(item.Quantity.ToString(), bodyFont, Brushes.Black, leftMargin + 140, y);
                g.DrawString(item.UnitPrice.ToString("N2"), bodyFont, Brushes.Black, leftMargin + 170, y);
                g.DrawString(item.LineTotal.ToString("N2"), bodyFont, Brushes.Black, leftMargin + 220, y);
                y += rowHeight + 2;
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

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

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
            
            var customerName = job.Customer?.FullName ?? "Belirtilmedi";
            if (customerName.Length > 24) customerName = customerName.Substring(0, 22) + "..";
            g.DrawString($"Müşteri  : {customerName}", bodyFont, Brushes.Black, leftMargin, y); y += 14;
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
            var descSize = g.MeasureString(desc, bodyFont, (int)width);
            var descHeight = Math.Min(Math.Max(24f, descSize.Height), 80f);
            g.DrawString(desc, bodyFont, Brushes.Black, new RectangleF(leftMargin, y, width, descHeight));
            y += descHeight + 6;

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
