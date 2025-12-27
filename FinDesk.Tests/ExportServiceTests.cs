using System;
using System.IO;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class ExportServiceTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly ExportService _exportService;

        public ExportServiceTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
            _exportService = new ExportService();
        }

        [Fact]
        public async Task ExportToCsvAsync_WritesHeadersAndRows()
        {
            var rows = new[]
            {
                new ReportRow { ["Date"] = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"), ["Amount"] = 10, ["Description"] = "Test" }
            };
            var options = new ExportOptions();

            var result = await _exportService.ExportToCsvAsync(rows, _tempFile, options);

            Assert.True(result);
            var content = await File.ReadAllTextAsync(_tempFile);
            Assert.Contains("Date", content);
            Assert.Contains("Test", content);
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
    }
}
