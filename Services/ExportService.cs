using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    public class ExportService
    {
        /// <summary>
        /// Експорт звіту у відповідному форматі з урахуванням налаштувань колонок і фільтрів.
        /// </summary>
        public Task<bool> ExportReportAsync(ReportResult result, string filePath, ExportFormat format, ExportOptions? options = null)
        {
            options ??= new ExportOptions();
            var rows = ApplyFilters(result.Rows, options.Filters);

            return format switch
            {
                ExportFormat.Csv => ExportToCsvAsync(rows, filePath, options),
                ExportFormat.Excel => ExportToExcelAsync(rows, filePath, options),
                ExportFormat.Pdf => ExportToPdfAsync(result, filePath, options),
                _ => Task.FromResult(false)
            };
        }

        // Експорт транзакцій у форматі CSV
        public async Task<bool> ExportToCsvAsync(IEnumerable<Transaction> transactions, string filePath)
        {
            var rows = transactions.Select(t =>
            {
                var row = new ReportRow();
                row["Date"] = t.TransactionDate;
                row["Description"] = t.Description;
                row["Amount"] = t.Amount;
                row["Category"] = t.Category;
                row["Account"] = t.Account;
                row["Balance"] = t.Balance;
                return row;
            });

            return await ExportToCsvAsync(rows, filePath, new ExportOptions());
        }

        public async Task<bool> ExportToCsvAsync(IEnumerable<ReportRow> rows, string filePath, ExportOptions options)
        {
            try
            {
                var normalizedRows = ApplyFilters(rows, options.Filters).ToList();
                if (!normalizedRows.Any())
                {
                    await File.WriteAllTextAsync(filePath, string.Empty, Encoding.UTF8);
                    return true;
                }

                var columns = ResolveColumns(normalizedRows, options.SelectedColumns);
                var delimiter = options.Delimiter;
                var builder = new StringBuilder();
                builder.AppendLine(string.Join(delimiter, columns));

                foreach (var row in normalizedRows)
                {
                    var values = columns.Select(c => EscapeCsv(row.Columns.TryGetValue(c, out var value) ? value : string.Empty, delimiter));
                    builder.AppendLine(string.Join(delimiter, values));
                }

                await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CSV Export Error: {ex.Message}");
                return false;
            }
        }

        // Експорт у формат Excel (XLSX) з використанням ClosedXML
        public async Task<bool> ExportToExcelAsync(IEnumerable<Transaction> transactions, string filePath)
        {
            var rows = transactions.Select(t =>
            {
                var row = new ReportRow();
                row["Date"] = t.TransactionDate;
                row["Description"] = t.Description;
                row["Amount"] = t.Amount;
                row["Category"] = t.Category;
                row["Account"] = t.Account;
                row["Balance"] = t.Balance;
                return row;
            });

            return await ExportToExcelAsync(rows, filePath, new ExportOptions());
        }

        public Task<bool> ExportToExcelAsync(IEnumerable<ReportRow> rows, string filePath, ExportOptions options)
        {
            return Task.Run(() =>
            {
                try
                {
                    var filtered = ApplyFilters(rows, options.Filters).ToList();
                    var columns = ResolveColumns(filtered, options.SelectedColumns);

                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.AddWorksheet("Report");

                    for (var i = 0; i < columns.Count; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = columns[i];
                    }

                    for (var rowIndex = 0; rowIndex < filtered.Count; rowIndex++)
                    {
                        var reportRow = filtered[rowIndex];
                        for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                        {
                            reportRow.Columns.TryGetValue(columns[colIndex], out var value);
                            
                            // Використовуємо типізований метод нормалізації
                            var cellValue = NormalizeCellValue(value);
                            
                            // ClosedXML v0.100+ вимагає присвоєння через властивість Value (типу XLCellValue)
                            worksheet.Cell(rowIndex + 2, colIndex + 1).Value = cellValue;
                        }
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Excel Export Error: {ex.Message}");
                    return false;
                }
            });
        }

        // Експорт у простий PDF (текстовий шаблон без зовнішніх залежностей)
        public async Task<bool> ExportToPdfAsync(ReportResult result, string filePath, ExportOptions options)
        {
            try
            {
                var filteredRows = ApplyFilters(result.Rows, options.Filters).ToList();
                var columns = ResolveColumns(filteredRows, options.SelectedColumns);
                var builder = new StringBuilder();

                builder.AppendLine(result.Title);
                builder.AppendLine($"Період: {result.From:yyyy-MM-dd} - {result.To:yyyy-MM-dd}");
                builder.AppendLine(new string('=', 48));
                builder.AppendLine(string.Join(" | ", columns));
                builder.AppendLine(new string('-', 48));

                foreach (var row in filteredRows)
                {
                    var values = columns.Select(c => row.Columns.TryGetValue(c, out var value) ? value?.ToString() ?? string.Empty : string.Empty);
                    builder.AppendLine(string.Join(" | ", values));
                }

                if (result.Charts.Any())
                {
                    builder.AppendLine();
                    builder.AppendLine("Charts:");
                    foreach (var chart in result.Charts)
                    {
                        builder.AppendLine($"- {chart.Title} ({chart.Type})");
                        foreach (var point in chart.Points)
                        {
                            builder.AppendLine($"  • {point.Label}: {point.Value}");
                        }
                    }
                }

                await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF Export Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportStatisticsAsync(Dictionary<string, decimal> statistics, string filePath)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Category,Amount");

                foreach (var stat in statistics)
                {
                    csv.AppendLine($"{stat.Key},{stat.Value}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Statistics Export Error: {ex.Message}");
                return false;
            }
        }

        private static string EscapeCsv(object? value, string delimiter)
        {
            var stringValue = value?.ToString() ?? string.Empty;
            if (stringValue.Contains(delimiter) || stringValue.Contains("\"") || stringValue.Contains("\n"))
            {
                return $"\"{stringValue.Replace("\"", "\"\"")}\"";
            }

            return stringValue;
        }

        private static List<string> ResolveColumns(IReadOnlyCollection<ReportRow> rows, IReadOnlyCollection<string> selectedColumns)
        {
            if (rows.Count == 0)
            {
                return selectedColumns.Count == 0 ? new List<string>() : selectedColumns.ToList();
            }

            return selectedColumns.Count > 0
                ? selectedColumns.ToList()
                : rows.First().Columns.Keys.ToList();
        }

        private static IEnumerable<ReportRow> ApplyFilters(IEnumerable<ReportRow> rows, Dictionary<string, string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return rows;
            }

            return rows.Where(r => filters.All(f => r.Columns.TryGetValue(f.Key, out var value) && string.Equals(value?.ToString(), f.Value, StringComparison.OrdinalIgnoreCase)));
        }

        // ВИПРАВЛЕНО: Повертаємо XLCellValue замість object для сумісності з ClosedXML
        private static XLCellValue NormalizeCellValue(object? value)
        {
            if (value == null) return Blank.Value;

            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
                decimal decimalValue => decimalValue,
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                long longValue => longValue, // ClosedXML має неявне перетворення
                string text => text,
                bool boolean => boolean,
                _ => value.ToString() ?? string.Empty
            };
        }

        // Імпорт транзакцій з CSV
        public async Task<List<Transaction>> ImportFromCsvAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var txService = new TransactionService(db, dedup);
            var service = new CsvImportService(db, new CategorizationService(db), txService);
            await service.ImportFromCsvAsync(filePath, cancellationToken: cancellationToken);
            return new List<Transaction>(); 
        }
    }
}
