using Avalonia.Controls;
using Avalonia.Input;
using FinDesk.Models;
using FinDesk.ViewModels;
using System.Linq;

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
        await vm.Import.HandleDropAsync(e);
    }

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel shell) return;
        if (shell.CurrentPage is not TransactionsViewModel tvm) return;

        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not Transaction t) return;

        if (cb.SelectedItem is MoneyCategory cat)
            await tvm.ChangeCategoryAsync(t, cat);
    }
}
