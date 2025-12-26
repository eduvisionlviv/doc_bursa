using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk.ViewModels;

public sealed partial class ImportViewModel : ViewModelBase
{
    private readonly Db _db;
    private readonly CategorizationService _categorizer;
    private readonly MainWindowViewModel _shell;

    public ObservableCollection<string> ImportedFiles { get; } = new();

    [ObservableProperty] private string hintText =
        "Перетягни сюди CSV/XLSX виписку або обери файл кнопкою нижче.";

    public ImportViewModel(Db db, CategorizationService categorizer, MainWindowViewModel shell)
    {
        _db = db;
        _categorizer = categorizer;
        _shell = shell;
    }

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "Statements", Extensions = { "csv", "xlsx" } }
            }
        };

        // Window is not directly accessible here; user can drag&drop instead.
        // For simplicity, show user guidance:
        HintText = "Скористайся Drag&Drop (перетягни файл на область імпорту).";
        await Task.CompletedTask;
    }

    public async Task HandleDropAsync(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;
        var files = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToList();
        if (files is null || files.Count == 0) return;

        foreach (var path in files)
            await ImportPathAsync(path);

        await _shell.RefreshAll();
    }

    private async Task ImportPathAsync(string path)
    {
        if (!File.Exists(path)) return;

        await _shell.SetStatusAsync("Імпорт…");
        var importer = new ImportService(_categorizer);
        var txs = await importer.ImportAsync(path, sourceLabel: "import");
        var inserted = await _db.UpsertTransactionsAsync(txs);

        ImportedFiles.Add($"{Path.GetFileName(path)} → +{inserted} нових");
        await _shell.SetStatusAsync("Готово");
    }
}
