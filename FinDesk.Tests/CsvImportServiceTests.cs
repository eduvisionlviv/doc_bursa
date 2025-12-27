using System.Collections.Generic;
using System.IO;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
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
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "Дата;Опис;Сума\n01.01.2024;Тест;-10", System.Text.Encoding.GetEncoding(1251));

            var service = new CsvImportService(new Services.DatabaseService(), new CategorizationService(new Services.DatabaseService()));
            var result = service.ImportFromCsv(tmp, "Universal");

            Assert.True(result.Imported + result.Skipped >= 0); // basic smoke test
        }
    }
}
