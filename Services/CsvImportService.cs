using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using FluentValidation;
using Serilog;

namespace doc_bursa.Services
{
    public class CsvImportService
    {
        private readonly DatabaseService _db;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorization;
        private readonly ILogger _logger;
        private readonly CsvRowValidator _validator;
        private static readonly Encoding[] _candidateEncodings =
        {
            new UTF8Encoding(false, true),
            new UTF8Encoding(true, true),
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            Encoding.UTF32,
            GetEncodingSafe(1251),
            Encoding.GetEncoding("iso-8859-1"),
            GetEncodingSafe(1252),
            GetEncodingSafe(866),
            Encoding.ASCII
        };

        private static Encoding GetEncodingSafe(int codePage)
        {
            try
            {
                // Цей рядок ОБОВ'ЯЗКОВИЙ для роботи з Windows-1251 у .NET Core/8
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(codePage);
            }
            catch (Exception)
            {
                return new UTF8Encoding(false, true);
            }
        }

        private static readonly string[] _dateFormats =
        {
            "dd.MM.yyyy",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "dd MMM yyyy",
            "yyyyMMdd",
            "dd-MM-yyyy",
            "yyyy/MM/dd",
            "dd.MM.yyyy HH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd.MM.yy",
            "yyyy.MM.dd",
            "M/d/yyyy",
            "MM/dd/yy",
            "dd/MM/yy",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "dd MMMM yyyy",
            "dd-MM-yyyy HH:mm",
            "yyyyMMddTHHmmss"
        };

        public CsvImportService(DatabaseService db, CategorizationService categorization, TransactionService transactionService)
        {
            _db = db;
            _categorization = categorization;
            _transactionService = transactionService;
            _logger = Log.ForContext<CsvImportService>();
            _validator = new CsvRowValidator();
        }

        public CsvImportResult ImportFromCsv(string filePath, string? bankType = null)
        {
            return ImportFromCsvAsync(filePath, bankType).GetAwaiter().GetResult();
        }

        public async Task<CsvImportResult> ImportFromCsvAsync(
            string filePath,
            string? bankType = null,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var usedEncoding = DetectEncodingWithFallback(filePath);
                using var reader = new StreamReader(filePath, usedEncoding, detectEncodingFromByteOrderMarks: true);

                var headerLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    return CsvImportResult.Error($"Файл порожній або не містить даних ({usedEncoding.WebName})");
                }

                var delimiter = DetectDelimiter(headerLine);
                var headers = SplitCsvLine(headerLine, delimiter)
                    .Select(h => h.Trim('\"', '\\', ' '))
                    .ToArray();

                var previewLines = await ReadPreviewAsync(reader, cancellationToken);
                var previewRows = previewLines
                    .Select(l => SplitCsvLine(l, delimiter))
                    .Where(r => r.Length > 0)
                    .ToList();

                var format = ResolveFormat(bankType, headers, previewRows);
                var profile = CsvFormatProfiles.GetProfile(format);
                var result = new CsvImportResult(0)
                {
                    EncodingUsed = usedEncoding.WebName,
                    Format = format.ToString()
                };

                const int batchSize = 1000;
                var batch = new List<Transaction>(batchSize);
                var processed = 0;

                foreach (var preview in previewLines)
                {
                    processed++;
                    if (!ProcessLine(preview, processed + 1, headers, delimiter, profile, format, batch, result, cancellationToken)
                        && batch.Count >= batchSize)
                    {
                        await FlushBatchAsync(batch, result, cancellationToken, progress, processed);
                    }
                }

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
                    processed++;
                    if (!ProcessLine(line, processed + 1, headers, delimiter, profile, format, batch, result, cancellationToken)
                        && batch.Count >= batchSize)
                    {
                        await FlushBatchAsync(batch, result, cancellationToken, progress, processed);
                    }
                }

                if (batch.Any())
                {
                    var saved = await _transactionService.AddTransactionsBatchAsync(batch, cancellationToken);
                    result.Imported += saved;
                }

                result.Total = processed;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CSV import failed");
                return CsvImportResult.Error(ex.Message);
            }
        }

        private static Encoding DetectEncodingWithFallback(string filePath)
        {
            var bomEncoding = DetectBomEncoding(filePath);
            if (bomEncoding != null)
            {
                return bomEncoding;
            }

            foreach (var enc in _candidateEncodings.Where(e => e != null))
            {
                if (IsEncodingReadable(filePath, enc!))
                {
                    return enc!;
                }
            }

            return new UTF8Encoding(false, true);
        }

        private static Encoding? DetectBomEncoding(string filePath)
        {
            var bom = new byte[4];
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Read(bom, 0, 4) < 2)
            {
                return null;
            }

            // BOM detection
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true, true);
            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00) return Encoding.UTF32;
            if (bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
            if (bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;

            return null;
        }

        private static bool IsEncodingReadable(string filePath, Encoding encoding)
        {
            try
            {
                using var reader = new StreamReader(filePath, encoding, detectEncodingFromByteOrderMarks: true);
                var buffer = new char[2048];
                var read = reader.ReadBlock(buffer, 0, buffer.Length);
                var sample = new string(buffer, 0, read);
                return !sample.Contains('\uFFFD');
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static char DetectDelimiter(string headerLine)
        {
            var candidates = new[] { ',', ';', '\t', '|' };
            return candidates
                .Select(c => new { Delimiter = c, Count = headerLine.Count(ch => ch == c) })
                .OrderByDescending(x => x.Count)
                .First().Delimiter;
        }

        private static CsvFormat ResolveFormat(string? bankType, string[] headers, List<string[]> previewRows)
        {
            if (!string.IsNullOrWhiteSpace(bankType) && Enum.TryParse<CsvFormat>(bankType, true, out var manualFormat))
            {
                return manualFormat;
            }

            return CsvFormatDetector.Detect(headers, previewRows);
        }

        private static string[] SplitCsvLine(string line, char delimiter)
        {
            var values = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == delimiter && !inQuotes)
                {
                    values.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }

            values.Add(sb.ToString());
            return values.ToArray();
        }

        private static Dictionary<string, string> BuildRowDictionary(string[] headers, string[] values)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                var key = headers[i].Trim();
                var value = i < values.Length ? values[i].Trim() : string.Empty;
                dict[key] = value;
            }

            return dict;
        }

        private bool ProcessLine(
            string line,
            int lineNumber,
            string[] headers,
            char delimiter,
            CsvFormatProfile profile,
            CsvFormat format,
            List<Transaction> batch,
            CsvImportResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = SplitCsvLine(line, delimiter);
            var rowDict = BuildRowDictionary(headers, values);
            var mapped = profile.Map(rowDict);

            if (mapped == null)
            {
                result.Skipped++;
                result.Errors.Add($"Рядок {lineNumber}: неможливо прочитати дані формату {format}");
                return true;
            }

            var validation = _validator.Validate(mapped);
            if (!validation.IsValid)
            {
                result.Skipped++;
                result.Errors.Add($"Рядок {lineNumber}: {string.Join(", ", validation.Errors.Select(e => e.ErrorMessage))}");
                return true;
            }

            if (!TryParseTransaction(mapped, format, out var transaction, out var parseError))
            {
                result.Skipped++;
                result.Errors.Add($"Рядок {lineNumber}: {parseError}");
                return true;
            }

            batch.Add(transaction);
            return false;
        }

        private bool TryParseTransaction(MappedCsvRow mapped, CsvFormat format, out Transaction transaction, out string error)
        {
            transaction = new Transaction();

            if (!DateTime.TryParseExact(mapped.Date, _dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDate)
                && !DateTime.TryParse(mapped.Date, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsedDate))
            {
                error = $"Неправильна дата: {mapped.Date}";
                return false;
            }

            if (!NormalizationHelper.TryNormalizeAmount(mapped.Amount, mapped.Currency, out var amount, out var currency))
            {
                error = $"Неправильна сума: {mapped.Amount}";
                return false;
            }

            transaction.Date = parsedDate;
            transaction.Description = mapped.Description;
            transaction.Amount = amount;
            transaction.Account = mapped.Account ?? string.Empty;
            transaction.Balance = mapped.Balance ?? 0m;
            transaction.Source = string.IsNullOrWhiteSpace(mapped.Source) ? format.ToString() : mapped.Source;
            transaction.Category = mapped.Category ?? string.Empty;
            transaction.TransactionId = mapped.TransactionId ?? $"{transaction.Source}-{transaction.Date:yyyyMMdd}-{Math.Abs(transaction.Description.GetHashCode())}";
            transaction.Hash = mapped.Hash ?? string.Empty;

            NormalizationHelper.NormalizeTransaction(transaction, currency);
            if (string.IsNullOrWhiteSpace(transaction.Category))
            {
                transaction.Category = _categorization.CategorizeTransaction(transaction);
            }

            error = string.Empty;
            return true;
        }

        private async Task FlushBatchAsync(List<Transaction> batch, CsvImportResult result, CancellationToken cancellationToken, IProgress<int>? progress, int processed)
        {
            var saved = await _transactionService.AddTransactionsBatchAsync(batch, cancellationToken);
            result.Imported += saved;
            batch.Clear();
            progress?.Report(processed);
        }

        private static async Task<List<string>> ReadPreviewAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            const int previewCount = 5;
            var lines = new List<string>(previewCount);
            while (!reader.EndOfStream && lines.Count < previewCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lines.Add(await reader.ReadLineAsync(cancellationToken) ?? string.Empty);
            }

            return lines;
        }
    }

    public enum CsvFormat
    {
        Monobank,
        PrivatBank,
        UkrsibBank,
        OtpBank,
        AlfaBank,
        Pumb,
        Raiffeisen,
        Wise,
        Revolut,
        Sber,
        Universal
    }

    public record MappedCsvRow
    {
        public string Date { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Amount { get; init; } = string.Empty;
        public string? Category { get; init; }
        public string? Currency { get; init; }
        public string? Account { get; init; }
        public decimal? Balance { get; init; }
        public string? Source { get; init; }
        public string? TransactionId { get; init; }
        public string? Hash { get; init; }
    }

    public class CsvRowValidator : AbstractValidator<MappedCsvRow>
    {
        public CsvRowValidator()
        {
            RuleFor(x => x.Date).NotEmpty().WithMessage("Дата відсутня");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Опис відсутній");
            RuleFor(x => x.Amount).NotEmpty().WithMessage("Сума відсутня");
        }
    }

    public class CsvImportResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> ProgressLog { get; } = new();
        public string EncodingUsed { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int Total { get; set; }

        public CsvImportResult(int total)
        {
            Total = total;
        }

        public static CsvImportResult Error(string message)
        {
            var result = new CsvImportResult(0);
            result.Errors.Add(message);
            return result;
        }
    }

    public static class CsvFormatDetector
    {
        private static readonly Dictionary<CsvFormat, string[]> HeaderSignatures = new()
        {
            { CsvFormat.Monobank, new[] { "дата", "опис", "сума", "валюта" } },
            { CsvFormat.PrivatBank, new[] { "час", "категорія", "опис" } },
            { CsvFormat.UkrsibBank, new[] { "balance", "description" } },
            { CsvFormat.Raiffeisen, new[] { "booking date", "value date" } },
            { CsvFormat.Revolut, new[] { "completed date", "reference" } },
            { CsvFormat.OtpBank, new[] { "account number", "otp" } },
            { CsvFormat.AlfaBank, new[] { "alfa", "дата операции" } },
            { CsvFormat.Pumb, new[] { "pumb", "merchant" } },
            { CsvFormat.Wise, new[] { "wise", "payee" } },
            { CsvFormat.Sber, new[] { "сбербанк", "назначение платежа" } }
        };

        private static readonly Dictionary<CsvFormat, string[]> ValueHints = new()
        {
            { CsvFormat.Monobank, new[] { "monobank", "mono" } },
            { CsvFormat.PrivatBank, new[] { "privat", "pb.ua" } },
            { CsvFormat.Raiffeisen, new[] { "raiffeisen", "aval" } },
            { CsvFormat.Revolut, new[] { "revolut", "vault" } },
            { CsvFormat.OtpBank, new[] { "otp" } },
            { CsvFormat.AlfaBank, new[] { "alfa", "a-bank" } },
            { CsvFormat.Pumb, new[] { "pumb" } },
            { CsvFormat.Wise, new[] { "wise", "transferwise" } },
            { CsvFormat.Sber, new[] { "sber", "сбербанк" } }
        };

        public static CsvFormat Detect(IEnumerable<string> headers, IEnumerable<string[]>? sampleRows = null)
        {
            var headerList = headers.Select(h => h.Trim().ToLowerInvariant()).ToList();
            var scored = new Dictionary<CsvFormat, int>();

            foreach (var signature in HeaderSignatures)
            {
                var score = signature.Value.Count(headerList.Contains);
                if (headerList.Any(h => signature.Value.Any(sig => h.Contains(sig))))
                {
                    score += 2;
                }

                scored[signature.Key] = score;
            }

            if (sampleRows != null)
            {
                foreach (var row in sampleRows)
                {
                    var lowerValues = row.Select(v => v.ToLowerInvariant());
                    foreach (var hint in ValueHints)
                    {
                        if (lowerValues.Any(v => hint.Value.Any(h => v.Contains(h))))
                        {
                            scored[hint.Key] = scored.GetValueOrDefault(hint.Key) + 1;
                        }
                    }
                }
            }

            var best = scored
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key.ToString(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return best.Value > 0 ? best.Key : CsvFormat.Universal;
        }
    }

    public static class CsvFormatProfiles
    {
        private static readonly Dictionary<CsvFormat, Func<Dictionary<string, string>, MappedCsvRow?>> _profiles =
            new()
            {
                { CsvFormat.Monobank, MapMonobank },
                { CsvFormat.PrivatBank, MapPrivatBank },
                { CsvFormat.UkrsibBank, MapUkrsib },
                { CsvFormat.OtpBank, MapOtp },
                { CsvFormat.AlfaBank, MapAlfa },
                { CsvFormat.Pumb, MapPumb },
                { CsvFormat.Raiffeisen, MapRaiffeisen },
                { CsvFormat.Wise, MapWise },
                { CsvFormat.Revolut, MapRevolut },
                { CsvFormat.Sber, MapSber },
                { CsvFormat.Universal, MapUniversal }
            };

        public static CsvFormatProfile GetProfile(CsvFormat format)
        {
            return new CsvFormatProfile(format, _profiles[format]);
        }

        private static string Get(Dictionary<string, string> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static MappedCsvRow? MapMonobank(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Дата", "date"),
                Description = Get(row, "Опис", "description"),
                Amount = Get(row, "Сума", "amount"),
                Currency = Get(row, "Валюта", "currency"),
                Source = "Monobank"
            };
        }

        private static MappedCsvRow? MapPrivatBank(Dictionary<string, string> row)
        {
            var date = Get(row, "Дата", "date");
            var time = Get(row, "Час", "time");
            return new MappedCsvRow
            {
                Date = string.IsNullOrWhiteSpace(time) ? date : $"{date} {time}",
                Description = Get(row, "Опис", "опис", "Description"),
                Amount = Get(row, "Сума", "amount"),
                Category = Get(row, "Категорія", "category"),
                Source = "PrivatBank"
            };
        }

        private static MappedCsvRow? MapUkrsib(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Date", "Дата"),
                Description = Get(row, "Description", "Опис"),
                Amount = Get(row, "Amount", "Сума"),
                Balance = decimal.TryParse(Get(row, "Balance"), NumberStyles.Any, CultureInfo.InvariantCulture, out var bal) ? bal : null,
                Source = "Ukrsibbank"
            };
        }

        private static MappedCsvRow? MapOtp(Dictionary<string, string> row)
        {
            var debit = Get(row, "Debit", "Дебет");
            var credit = Get(row, "Credit", "Кредит");
            var amount = !string.IsNullOrWhiteSpace(debit) ? $"-{debit}" : credit;
            return new MappedCsvRow
            {
                Date = Get(row, "Transaction date", "Дата"),
                Description = Get(row, "Details", "Опис"),
                Amount = amount,
                Currency = Get(row, "Currency", "Валюта"),
                Account = Get(row, "Account number", "Рахунок"),
                Source = "OTP Bank"
            };
        }

        private static MappedCsvRow? MapAlfa(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Дата операции", "Дата"),
                Description = Get(row, "Описание", "Description"),
                Amount = Get(row, "Сумма в валюте операции", "Amount"),
                Currency = Get(row, "Валюта", "Currency"),
                Source = "Alfa Bank"
            };
        }

        private static MappedCsvRow? MapPumb(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Date", "Дата"),
                Description = Get(row, "Merchant", "Description", "Опис"),
                Amount = Get(row, "Amount", "Сума"),
                Account = Get(row, "Account", "Рахунок"),
                Source = "PUMB"
            };
        }

        private static MappedCsvRow? MapRaiffeisen(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Booking date", "Value date", "Дата"),
                Description = Get(row, "Transaction details", "Details", "Опис"),
                Amount = Get(row, "Amount", "Сума"),
                Currency = Get(row, "Currency"),
                Source = "Raiffeisen"
            };
        }

        private static MappedCsvRow? MapWise(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Date", "Дата"),
                Description = Get(row, "Payee Name", "Description"),
                Amount = Get(row, "Amount", "Сума"),
                Currency = Get(row, "Currency"),
                Source = "Wise"
            };
        }

        private static MappedCsvRow? MapRevolut(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Completed Date", "Started Date", "Date"),
                Description = Get(row, "Reference", "Description"),
                Amount = Get(row, "Amount", "Сума"),
                Currency = Get(row, "Currency"),
                Source = "Revolut"
            };
        }

        private static MappedCsvRow? MapSber(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Дата операции", "Дата"),
                Description = Get(row, "Описание", "Назначение платежа"),
                Amount = Get(row, "Сумма", "Amount"),
                Source = "Sber"
            };
        }

        private static MappedCsvRow? MapUniversal(Dictionary<string, string> row)
        {
            return new MappedCsvRow
            {
                Date = Get(row, "Дата", "Date"),
                Description = Get(row, "Опис", "Description"),
                Amount = Get(row, "Сума", "Amount"),
                Source = "CSV Import"
            };
        }
    }

    public class CsvFormatProfile
    {
        public CsvFormat Format { get; }
        private readonly Func<Dictionary<string, string>, MappedCsvRow?> _mapper;

        public CsvFormatProfile(CsvFormat format, Func<Dictionary<string, string>, MappedCsvRow?> mapper)
        {
            Format = format;
            _mapper = mapper;
        }

        public MappedCsvRow? Map(Dictionary<string, string> row) => _mapper(row);
    }
}
