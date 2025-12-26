using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class ExportService
    {
        // Експорт транзакцій у форматі CSV
        public async Task<bool> ExportToCsvAsync(IEnumerable<Transaction> transactions, string filePath)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Date,Description,Amount,Category,Account,Balance");

                foreach (var t in transactions)
                {
                    csv.AppendLine($"{t.TransactionDate:yyyy-MM-dd},{EscapeCsv(t.Description)},{t.Amount},{EscapeCsv(t.Category)},{EscapeCsv(t.Account)},{t.Balance}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CSV Export Error: {ex.Message}");
                return false;
            }
        }

        // Експорт транзакцій у форматі Excel (XLSX)
        public async Task<bool> ExportToExcelAsync(IEnumerable<Transaction> transactions, string filePath)
        {
            try
            {
                // TODO: Інтегрувати з бібліотекою EPPlus або ClosedXML для створення XLSX
                // Поки що створюємо CSV файл з розширенням .xlsx (базова реалізація)
                var csv = new StringBuilder();
                csv.AppendLine("Date\tDescription\tAmount\tCategory\tAccount\tBalance");

                foreach (var t in transactions)
                {
                    csv.AppendLine($"{t.TransactionDate:yyyy-MM-dd}\t{t.Description}\t{t.Amount}\t{t.Category}\t{t.Account}\t{t.Balance}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excel Export Error: {ex.Message}");
                return false;
            }
        }

        // Експорт звіту у форматі PDF
        public async Task<bool> ExportToPdfAsync(IEnumerable<Transaction> transactions, string filePath, string title = "Transaction Report")
        {
            try
            {
                // TODO: Інтегрувати з бібліотекою iTextSharp або PdfSharp для створення PDF
                // Поки що створюємо текстовий звіт
                var report = new StringBuilder();
                report.AppendLine(title);
                report.AppendLine(new string('=', 80));
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                report.AppendLine(new string('-', 80));
                report.AppendLine();

                foreach (var t in transactions)
                {
                    report.AppendLine($"Date: {t.TransactionDate:yyyy-MM-dd}");
                    report.AppendLine($"Description: {t.Description}");
                    report.AppendLine($"Amount: {t.Amount:N2}");
                    report.AppendLine($"Category: {t.Category}");
                    report.AppendLine($"Account: {t.Account}");
                    report.AppendLine($"Balance: {t.Balance:N2}");
                    report.AppendLine(new string('-', 80));
                }

                await File.WriteAllTextAsync(filePath, report.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF Export Error: {ex.Message}");
                return false;
            }
        }

        // Експорт статистики по категоріях
        public async Task<bool> ExportCategoryStatsToCsvAsync(Dictionary<string, decimal> stats, string filePath)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Category,Total Amount");

                foreach (var stat in stats.OrderByDescending(x => Math.Abs(x.Value)))
                {
                    csv.AppendLine($"{EscapeCsv(stat.Key)},{stat.Value:N2}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Category Stats Export Error: {ex.Message}");
                return false;
            }
        }

        // Допоміжна функція для екранування CSV полів
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
