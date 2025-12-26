using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class FileImportService
    {
        public List<Transaction> ImportFile(string filePath, string source)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".csv" => ImportCsv(filePath, source),
                ".xlsx" => ImportExcel(filePath, source),
                _ => new List<Transaction>()
            };
        }

        private List<Transaction> ImportCsv(string filePath, string source)
        {
            var transactions = new List<Transaction>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2) return transactions;

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 3) continue;

                    var transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        Date = DateTime.TryParse(parts[0], out var date) ? date : DateTime.Now,
                        Amount = decimal.TryParse(parts[1], out var amount) ? amount : 0,
                        Description = parts.Length > 2 ? parts[2] : "",
                        Source = source
                    };
                    transaction.Hash = ComputeHash(transaction);
                    transactions.Add(transaction);
                }
            }
            catch { }

            return transactions;
        }

        private List<Transaction> ImportExcel(string filePath, string source)
        {
            var transactions = new List<Transaction>();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.First();

                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    var transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        Date = row.Cell(1).TryGetValue(out DateTime date) ? date : DateTime.Now,
                        Amount = row.Cell(2).TryGetValue(out decimal amount) ? amount : 0,
                        Description = row.Cell(3).GetValue<string>(),
                        Source = source
                    };
                    transaction.Hash = ComputeHash(transaction);
                    transactions.Add(transaction);
                }
            }
            catch { }

            return transactions;
        }

        private string ComputeHash(Transaction t)
        {
            var data = $"{t.TransactionId}|{t.Date:O}|{t.Amount}|{t.Source}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(bytes);
        }
    }
}
