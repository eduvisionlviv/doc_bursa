using System;
using System.IO;
using System.Windows;
using FinDesk.Services;
using Serilog;
using Serilog.Events;

namespace FinDesk
{
    public partial class App : Application
    {
        public static string AppDataPath { get; private set; } = string.Empty;
        private DeduplicationBackgroundTask? _deduplicationBackgroundTask;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinDesk"
            );

            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }

            ConfigureLogging();
            Log.Information("FinDesk application starting.");

            // Запуск фонової дедуплікації
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var txService = new TransactionService(db, dedup);
            _deduplicationBackgroundTask = new DeduplicationBackgroundTask(txService);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _deduplicationBackgroundTask?.Dispose();
            Log.Information("FinDesk application shutting down.");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private static void ConfigureLogging()
        {
            var logsPath = Path.Combine(AppDataPath, "logs");
            Directory.CreateDirectory(logsPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    Path.Combine(logsPath, "findesk-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true)
                .CreateLogger();
        }
    }
}
