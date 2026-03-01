using ClosedXML.Excel;
using Serilog;

namespace KamatekCrm.API.Services
{
    /// <summary>
    /// Excel Export/Import Engine — ClosedXML ile
    /// Generic yapıda: herhangi bir entity listesini Excel'e çevirebilir.
    /// </summary>
    public interface IExcelService
    {
        /// <summary>Generic liste → Excel workbook bytes</summary>
        byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Veri", ExcelExportOptions? options = null);

        /// <summary>Önceden tanımlı rapor şablonlarını export et</summary>
        byte[] ExportServiceJobs(IEnumerable<ServiceJobExportRow> jobs);
        byte[] ExportCustomers(IEnumerable<CustomerExportRow> customers);
    }

    public class ExcelService : IExcelService
    {
        public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Veri", ExcelExportOptions? options = null)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var properties = typeof(T).GetProperties()
                .Where(p => options?.ExcludedColumns == null || !options.ExcludedColumns.Contains(p.Name))
                .ToList();

            // Header
            for (int i = 0; i < properties.Count; i++)
            {
                var displayName = options?.ColumnNames?.GetValueOrDefault(properties[i].Name) ?? properties[i].Name;
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = displayName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            int row = 2;
            foreach (var item in data)
            {
                for (int col = 0; col < properties.Count; col++)
                {
                    var value = properties[col].GetValue(item);
                    var cell = worksheet.Cell(row, col + 1);

                    if (value is DateTime dt)
                        cell.Value = dt.ToString("dd.MM.yyyy HH:mm");
                    else if (value is decimal d)
                    {
                        cell.Value = d;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    }
                    else if (value is double dbl)
                    {
                        cell.Value = dbl;
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    }
                    else
                        cell.Value = value?.ToString() ?? "";

                    // Zebra striping
                    if (row % 2 == 0)
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                }
                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Freeze header row
            worksheet.SheetView.FreezeRows(1);

            // Add auto-filter
            if (row > 2)
                worksheet.Range(1, 1, row - 1, properties.Count).SetAutoFilter();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            Log.Information("Excel exported: {SheetName}, {RowCount} rows, {ColCount} columns",
                sheetName, row - 2, properties.Count);

            return stream.ToArray();
        }

        /// <summary>
        /// Servis işleri özel formatla export
        /// </summary>
        public byte[] ExportServiceJobs(IEnumerable<ServiceJobExportRow> jobs)
        {
            return ExportToExcel(jobs, "Servis İşleri", new ExcelExportOptions
            {
                ColumnNames = new Dictionary<string, string>
                {
                    ["Id"] = "İş No",
                    ["Title"] = "Başlık",
                    ["CustomerName"] = "Müşteri",
                    ["Status"] = "Durum",
                    ["Priority"] = "Öncelik",
                    ["AssignedTechnician"] = "Teknisyen",
                    ["CreatedDate"] = "Oluşturma Tarihi",
                    ["CompletedDate"] = "Tamamlanma Tarihi",
                    ["Price"] = "Tutar (₺)",
                    ["LaborCost"] = "İşçilik (₺)"
                }
            });
        }

        /// <summary>
        /// Müşteri listesi özel formatla export
        /// </summary>
        public byte[] ExportCustomers(IEnumerable<CustomerExportRow> customers)
        {
            return ExportToExcel(customers, "Müşteriler", new ExcelExportOptions
            {
                ColumnNames = new Dictionary<string, string>
                {
                    ["Id"] = "Müşteri No",
                    ["FullName"] = "Ad Soyad",
                    ["PhoneNumber"] = "Telefon",
                    ["Email"] = "E-posta",
                    ["Address"] = "Adres",
                    ["CreatedDate"] = "Kayıt Tarihi",
                    ["TotalJobs"] = "Toplam İş"
                }
            });
        }
    }

    #region Export Options & DTOs

    public class ExcelExportOptions
    {
        /// <summary>Kolon adı eşlemeleri (Property → Display Name)</summary>
        public Dictionary<string, string>? ColumnNames { get; set; }

        /// <summary>Export'tan hariç tutulacak kolonlar</summary>
        public HashSet<string>? ExcludedColumns { get; set; }
    }

    public class ServiceJobExportRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string AssignedTechnician { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal Price { get; set; }
        public decimal LaborCost { get; set; }
    }

    public class CustomerExportRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedDate { get; set; }
        public int TotalJobs { get; set; }
    }

    #endregion
}
