using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using OfficeOpenXml;
using Serilog;

namespace doc_bursa.Services
{
    public class ExcelImportService
    {
        private readonly DatabaseService _db;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorization;
        private readonly ILogger _logger;

        public ExcelImportService(DatabaseService db, CategorizationService categorization, TransactionService transactionService)
        {
            _db = db;
            _categorization = categorization;
            _transactionService = transactionService;
            _logger = Log.ForContext<ExcelImportService>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<CsvImportResult> ImportFromExcelAsync(
            string filePath,
            string? bankType = null,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                using var package = new ExcelPackage(new FileInfo(filePath));
                var worksheet = package.Workbook.Worksheets[0];
                
                if (worksheet.Dimension == null)
                {
                    return CsvImportResult.Error("Файл XLSX порожній");
                }

                var rows = worksheet.Dimension.End.Row;
                var cols = worksheet.Dimension.End.Column;

                var headers = new string[cols];
                for (int col = 1; col <= cols; col++)
                {
                    headers[col - 1] = worksheet.Cells[1, col].Text?.Trim() ?? string.Empty;
                }

                var format = CsvFormatDetector.Detect(headers);
                var profile = CsvFormatProfiles.GetProfile(format);
                var result = new CsvImportResult(rows - 1)
                {
                    EncodingUsed = "UTF-8",
                    Format = format.ToString()
                };

                const int batchSize = 1000;
                var batch = new List<Transaction>(batchSize);

                for (int row = 2; row <= rows; row++)
                {
                    ct.ThrowIfCancellationRequested();

                    var rowDict = new Dictionary<string, string>();
                    for (int col = 1; col <= cols; col++)
                    {
                        var value = worksheet.Cells[row, col].Text?.Trim() ?? string.Empty;
                        rowDict[headers[col - 1]] = value;
                    }

                    var mapped = profile.Map(rowDict);
                    if (mapped == null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (TryParseTransaction(mapped, format, out var transaction, out _))
                    {
                        batch.Add(transaction);
                    }
                    else
                    {
                        result.Skipped++;
                    }

                    if (batch.Count >= batchSize)
                    {
                        var saved = await _transactionService.AddTransactionsBatchAsync(batch, ct);
                        result.Imported += saved;
                        batch.Clear();
                        progress?.Report(row - 1);
                    }
                }

                if (batch.Any())
                {
                    var saved = await _transactionService.AddTransactionsBatchAsync(batch, ct);
                    result.Imported += saved;
                }

                result.Total = rows - 1;
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Excel import failed");
                return CsvImportResult.Error(ex.Message);
            }
        }

        private bool TryParseTransaction(MappedCsvRow mapped, CsvFormat format, out Transaction transaction, out string error)
        {
            transaction = new Transaction();

            var dateFormats = new[]
            {
                "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy",
                "dd.MM.yyyy HH:mm", "yyyy-MM-ddTHH:mm:ss"
            };

            if (!DateTime.TryParseExact(mapped.Date, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                error = $"Invalid date: {mapped.Date}";
                return false;
            }

            if (!decimal.TryParse(mapped.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                error = $"Invalid amount: {mapped.Amount}";
                return false;
            }

            transaction.Date = parsedDate;
            transaction.Description = mapped.Description;
            transaction.Amount = amount;
            transaction.Account = mapped.Account ?? string.Empty;
            transaction.Balance = mapped.Balance ?? 0m;
            transaction.Source = mapped.Source ?? format.ToString();
            transaction.Category = !string.IsNullOrWhiteSpace(mapped.Category)
                ? mapped.Category
                : _categorization.CategorizeTransaction(transaction);
            transaction.TransactionId = mapped.TransactionId ?? $"{transaction.Source}-{transaction.Date:yyyyMMdd}-{Math.Abs(transaction.Description.GetHashCode())}";
            transaction.Hash = mapped.Hash ?? string.Empty;

            error = string.Empty;
            return true;
        }
    }
}
