using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FinDesk.Models;
using doc_bursa.Models;

namespace FinDesk.Services
{
    public class CsvImportService
    {
        private readonly DatabaseService _db;
        private readonly CategorizationService _categorization;

        public CsvImportService(DatabaseService db, CategorizationService categorization)
        {
            _db = db;
            _categorization = categorization;
        }

        public (int imported, int skipped, string error) ImportFromCsv(string filePath, string bankType)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2) return (0, 0, "Файл порожній або не містить даних");

                var imported = 0;
                var skipped = 0;

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var transaction = ParseCsvLine(lines[i], bankType);
                        if (transaction != null && _db.AddTransaction(transaction))
                        {
                            imported++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                return (imported, skipped, string.Empty);
            }
            catch (Exception ex)
            {
                return (0, 0, ex.Message);
            }
        }

        private Transaction? ParseCsvLine(string line, string bankType)
        {
            var parts = line.Split(',', ';');
            if (parts.Length < 3) return null;

            return bankType.ToLower() switch
            {
                "monobank" => ParseMonobankCsv(parts),
                "privatbank" => ParsePrivatBankCsv(parts),
                "ukrsibbank" => ParseUkrsibBankCsv(parts),
                "universal" => ParseUniversalCsv(parts),
                _ => ParseUniversalCsv(parts)
            };
        }

        private Transaction? ParseMonobankCsv(string[] parts)
        {
            // Формат Monobank CSV: Дата,Опис,Сума,Валюта
            if (parts.Length < 3) return null;

            return new Transaction
            {
                Date = DateTime.Parse(parts[0]),
                Description = parts[1].Trim('"'),
                Amount = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                Source = "Monobank",
                Category = "Інше",
                TransactionId = $"mono_{DateTime.Now.Ticks}_{parts[1].GetHashCode()}"
            };
        }

        private Transaction? ParsePrivatBankCsv(string[] parts)
        {
            // Формат PrivatBank CSV: Дата,Час,Категорія,Опис,Сума
            if (parts.Length < 5) return null;

            return new Transaction
            {
                Date = DateTime.Parse($"{parts[0]} {parts[1]}"),
                Description = parts[3].Trim('"'),
                Amount = decimal.Parse(parts[4], CultureInfo.InvariantCulture),
                Source = "PrivatBank",
                Category = parts[2].Trim('"'),
                TransactionId = $"pb_{DateTime.Now.Ticks}_{parts[3].GetHashCode()}"
            };
        }

        private Transaction? ParseUkrsibBankCsv(string[] parts)
        {
            // Формат Ukrsibbank CSV: Дата,Опис,Сума,Баланс
            if (parts.Length < 3) return null;

            return new Transaction
            {
                Date = DateTime.Parse(parts[0]),
                Description = parts[1].Trim('"'),
                Amount = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                Source = "Ukrsibbank",
                Category = "Інше",
                TransactionId = $"usb_{DateTime.Now.Ticks}_{parts[1].GetHashCode()}"
            };
        }

        private Transaction? ParseUniversalCsv(string[] parts)
        {
            // Універсальний формат: Дата,Опис,Сума
            if (parts.Length < 3) return null;

            return new Transaction
            {
                Date = DateTime.Parse(parts[0]),
                Description = parts[1].Trim('"'),
                Amount = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                Source = "CSV Import",
                Category = "Інше",
                TransactionId = $"csv_{DateTime.Now.Ticks}_{parts[1].GetHashCode()}"
            };
        }
    }
}
