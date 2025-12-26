using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace FinDesk;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = await SettingsService.LoadAsync();
            var db = new Db(settings);
            await db.InitAsync();

            var categorizer = new CategorizationService(db);
            var analytics = new AnalyticsService(db);
            var mono = new MonobankClient();

            var vm = new MainWindowViewModel(settings, db, categorizer, analytics, mono);
            desktop.MainWindow = new MainWindow { DataContext = vm };

            await Task.Delay(50);
            await vm.RefreshAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
