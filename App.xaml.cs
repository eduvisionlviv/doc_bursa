using System;
using System.IO;
using System.Windows;
using doc_bursa.Services;
using Serilog;
using Serilog.Events;

namespace doc_bursa
{
    public partial class App : Application
    {
        public static string AppDataPath { get; private set; } = string.Empty;
        private DeduplicationBackgroundTask? _deduplicationBackgroundTask;
        private SyncEngineService? _syncEngineService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "doc_bursa"
            );

            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }

            ConfigureLogging();
            Log.Information("doc_bursa application starting.");

            // Запуск фонової дедуплікації
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var categorization = new CategorizationService(db);
            var txService = new TransactionService(db, dedup, categorization);
            _deduplicationBackgroundTask = new DeduplicationBackgroundTask(txService);
            _syncEngineService = new SyncEngineService(db, txService);
            _syncEngineService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _syncEngineService?.Dispose();
            _deduplicationBackgroundTask?.Dispose();
            Log.Information("doc_bursa application shutting down.");
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
