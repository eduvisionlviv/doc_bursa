using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDesk.Services
{
    public class ImportLogService
    {
        private readonly string _logDirectory;

        public ImportLogService()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);
        }

        public async Task SaveImportLogAsync(CsvImportResult result, string filePath)
        {
            var logFileName = $"Import_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            var logPath = Path.Combine(_logDirectory, logFileName);

            var log = new StringBuilder();
            log.AppendLine($"📄 Import Log");
            log.AppendLine($"============================================");
            log.AppendLine($"File: {Path.GetFileName(filePath)}");
            log.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine($"Format: {result.Format}");
            log.AppendLine($"Encoding: {result.EncodingUsed}");
            log.AppendLine($"Total Rows: {result.Total}");
            log.AppendLine($"Imported: {result.Imported}");
            log.AppendLine($"Skipped: {result.Skipped}");
            log.AppendLine($"Success Rate: {(result.Total > 0 ? (result.Imported * 100.0 / result.Total):0):F2}%");
            log.AppendLine();

            if (result.Errors.Any())
            {
                log.AppendLine($"⚠️ Errors ({result.Errors.Count}):");
                log.AppendLine($"============================================");
                foreach (var error in result.Errors.Take(100))
                {
                    log.AppendLine($"  • {error}");
                }
                if (result.Errors.Count > 100)
                {
                    log.AppendLine($"  ... and {result.Errors.Count - 100} more errors");
                }
            }
            else
            {
                log.AppendLine("✅ No errors!");
            }

            await File.WriteAllTextAsync(logPath, log.ToString());
        }

        public string[] GetRecentLogs(int count = 10)
        {
            var logFiles = Directory.GetFiles(_logDirectory, "Import_*.log")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Take(count)
                .ToArray();

            return logFiles;
        }
    }
}

