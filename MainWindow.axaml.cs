using Avalonia.Controls;
using Avalonia.Input;
using FinDesk;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
        if (files is null || files.Length == 0) return;

        await vm.ImportFilesAsync(files);
    }

    private async void OnChooseImportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var dlg = new OpenFileDialog
        {
            AllowMultiple = true,
            Filters =
            {
                new FileDialogFilter { Name = "Statements", Extensions = { "csv", "xlsx" } }
            }
        };

        var picked = await dlg.ShowAsync(this);
        if (picked is null || picked.Length == 0) return;

        await vm.ImportFilesAsync(picked);
    }

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not TransactionRow row) return;
        if (cb.SelectedItem is not MoneyCategory cat) return;

        await vm.SetCategoryAsync(row, cat);
    }
}
