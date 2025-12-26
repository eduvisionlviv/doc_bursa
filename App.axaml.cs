using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FinDesk.Services;
using FinDesk.ViewModels;

namespace FinDesk;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Bootstrap services
            var settings = await SettingsService.LoadAsync();
            var db = new Db(settings);
            await db.InitAsync();

            var categorizer = new CategorizationService(db);
            var analytics = new AnalyticsService(db);

            var monobank = new MonobankClient();
            var privat = new PrivatAutoclientClient();   // skeleton + fallback strategy in UI
            var ukrsib = new UkrsibClientStub();         // stub: import-first per requirements

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    settings, db, categorizer, analytics, monobank, privat, ukrsib)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
