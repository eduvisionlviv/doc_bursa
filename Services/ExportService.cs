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
                // TODO: Інтегрувати з бібліотекою EPPlus або ClosedXML
                // Поки що створюємо CSV файл з розширенням .xlsx
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

        // Експорт транзакцій у форматі PDF
        public async Task<bool> ExportToPdfAsync(IEnumerable<Transaction> transactions, string filePath)
        {
            // TODO: Реалізувати експорт у PDF
            await Task.CompletedTask;
            return false;
        }

        // Експорт статистики
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

        // Допоміжний метод для екранування CSV значень
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        // Імпорт транзакцій з CSV
        public Task<List<Transaction>> ImportFromCsvAsync(string filePath)
        {
            var service = new CsvImportService(new DatabaseService(), new CategorizationService(new DatabaseService()));
            var result = service.ImportFromCsv(filePath);
            return Task.FromResult(new List<Transaction>()); // keeping signature but functionality moved to CsvImportService
        }
    }
}
