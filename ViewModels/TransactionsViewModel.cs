using CommunityToolkit.Mvvm.ComponentModel;
using FinDesk.Models;
using FinDesk.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk.ViewModels;

public sealed partial class TransactionsViewModel : ViewModelBase
{
    private readonly Db _db;
    private readonly CategorizationService _categorizer;
    private readonly MainWindowViewModel _shell;

    public ObservableCollection<Transaction> Items { get; } = new();
    public ObservableCollection<MoneyCategory> Categories { get; } = new();

    public TransactionsViewModel(Db db, CategorizationService categorizer, MainWindowViewModel shell)
    {
        _db = db;
        _categorizer = categorizer;
        _shell = shell;

        foreach (var c in _categorizer.AllCategories())
            Categories.Add(c);
    }

    public async Task RefreshAsync(DateTime fromUtc, DateTime toUtc)
    {
        Items.Clear();
        var txs = await _db.GetTransactionsAsync(fromUtc, toUtc);
        foreach (var t in txs) Items.Add(t);
    }

    public async Task ChangeCategoryAsync(Transaction t, MoneyCategory newCat)
    {
        if (t.Category == newCat) return;

        t.Category = newCat;
        await _db.UpdateTransactionCategoryAsync(t.Id, newCat);

        var key = _categorizer.NormalizeMerchant(t.Merchant.Length > 0 ? t.Merchant : t.Description);
        if (!string.IsNullOrWhiteSpace(key))
            await _db.SaveMerchantCategoryAsync(key, newCat);

        await _shell.RefreshAll();
    }
}
