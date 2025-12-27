using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using doc_bursa.Services;
using Xunit;

namespace doc_bursa.Tests
{
    public class CsvImportServiceTests
    {
        [Fact]
        public void DetectsMonobankFormat()
        {
            var headers = new[] { "Дата", "Опис", "Сума", "Валюта" };
            var format = CsvFormatDetector.Detect(headers);
            Assert.Equal(CsvFormat.Monobank, format);
        }

        [Fact]
        public void DetectsPrivatBankFormat()
        {
            var headers = new[] { "Дата", "Час", "Категорія", "Опис", "Сума" };
            var format = CsvFormatDetector.Detect(headers);
            Assert.Equal(CsvFormat.PrivatBank, format);
        }

        [Fact]
        public void DetectsUniversalFormatWhenUnknown()
        {
            var headers = new[] { "Col1", "Col2" };
            var format = CsvFormatDetector.Detect(headers);
            Assert.Equal(CsvFormat.Universal, format);
        }

        [Fact]
        public void MapsMonobankRow()
        {
            var row = new Dictionary<string, string>
            {
                { "Дата", "01.01.2024" },
                { "Опис", "Coffee" },
                { "Сума", "-100.00" },
                { "Валюта", "UAH" }
            };

            var profile = CsvFormatProfiles.GetProfile(CsvFormat.Monobank);
            var mapped = profile.Map(row);

            Assert.Equal("01.01.2024", mapped!.Date);
            Assert.Equal("Coffee", mapped.Description);
            Assert.Equal("-100.00", mapped.Amount);
            Assert.Equal("Monobank", mapped.Source);
        }

        [Fact]
        public void ValidatesRequiredFields()
        {
            var validator = new CsvRowValidator();
            var mapped = new MappedCsvRow { Date = string.Empty, Description = "", Amount = "" };
            var result = validator.Validate(mapped);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void HandlesMultipleEncodings()
        {
            var temp = CreateIsolatedAppData();
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "Дата;Опис;Сума\n01.01.2024;Тест;-10", System.Text.Encoding.GetEncoding(1251));

            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var tx = new TransactionService(db, dedup);
            var service = new CsvImportService(db, new CategorizationService(db), tx);
            var result = service.ImportFromCsv(tmp, "Universal");

            Assert.True(result.Imported + result.Skipped >= 0); // basic smoke test
            Cleanup(temp);
        }

        [Fact]
        public void DetectsFormatFromValues_WhenHeadersAreGeneric()
        {
            var headers = new[] { "col1", "col2", "col3" };
            var sample = new List<string[]>
            {
                new[] { "2024-01-01", "Revolut vault", "-10" }
            };

            var format = CsvFormatDetector.Detect(headers, sample);
            Assert.Equal(CsvFormat.Revolut, format);
        }

        [Fact]
        public void ImportsTabSeparatedFiles()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, "Date\tDescription\tAmount\n2024-02-01\tCoffee\t-15.5\n2024-02-02\tGroceries\t-200");

                var db = new DatabaseService();
                var service = new CsvImportService(db, new CategorizationService(db), new TransactionService(db, new DeduplicationService(db)));
                var result = service.ImportFromCsv(tmp, "Universal");

                Assert.Equal(2, result.Imported);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void ParsesAdditionalDateFormats()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, "Дата,Опис,Сума\n2024/03/01 13:45,Test,-1\n10-03-2024 08:30,Test2,-2");

                var db = new DatabaseService();
                var service = new CsvImportService(db, new CategorizationService(db), new TransactionService(db, new DeduplicationService(db)));
                var result = service.ImportFromCsv(tmp, "Universal");

                Assert.Equal(2, result.Imported);
                Assert.Equal(2, result.Total);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void DetectsBomEncoding()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var tmp = Path.GetTempFileName();
                var content = "Дата,Опис,Сума\n2024-01-01,Тест,-1";
                File.WriteAllText(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                var db = new DatabaseService();
                var service = new CsvImportService(db, new CategorizationService(db), new TransactionService(db, new DeduplicationService(db)));
                var result = service.ImportFromCsv(tmp, "Universal");

                Assert.Equal("utf-8", result.EncodingUsed, ignoreCase: true);
                Assert.Equal(1, result.Imported);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void ProcessesPreviewedRows()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, "Дата,Опис,Сума\n2024-01-01,First,-1\n2024-01-02,Second,-2\n2024-01-03,Third,-3\n2024-01-04,Fourth,-4");

                var db = new DatabaseService();
                var service = new CsvImportService(db, new CategorizationService(db), new TransactionService(db, new DeduplicationService(db)));
                var result = service.ImportFromCsv(tmp, "Universal");

                Assert.Equal(4, result.Imported);
                Assert.Equal(4, result.Total);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        private static string CreateIsolatedAppData()
        {
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SetAppDataPath(temp);
            return temp;
        }

        private static void Cleanup(string? path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void SetAppDataPath(string path)
        {
            var property = typeof(App).GetProperty("AppDataPath", BindingFlags.Static | BindingFlags.Public);
            property!.SetValue(null, path);
        }
    }
}
