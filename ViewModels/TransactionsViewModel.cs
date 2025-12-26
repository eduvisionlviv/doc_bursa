using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.ViewModels
{
    public partial class TransactionsViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly CategorizationService _categorization;

        [ObservableProperty]
        private ObservableCollection<Transaction> transactions = new();

        [ObservableProperty]
        private Transaction? selectedTransaction;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedCategory = "Всі";

        public List<string> Categories { get; } = new()
        {
            "Всі", "Продукти", "Транспорт", "Ресторани", "Здоров'я", "Розваги", "Дохід", "Інше"
        };

        public TransactionsViewModel()
        {
            _db = new DatabaseService();
            _categorization = new CategorizationService(_db);
            LoadTransactions();
        }

        [RelayCommand]
        private void LoadTransactions()
        {
            var allTransactions = _db.GetTransactions();

            if (!string.IsNullOrEmpty(SearchText))
            {
                allTransactions = allTransactions
                    .Where(t => t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (SelectedCategory != "Всі")
            {
                allTransactions = allTransactions.Where(t => t.Category == SelectedCategory).ToList();
            }

            Transactions = new ObservableCollection<Transaction>(allTransactions);
        }

        [RelayCommand]
        private void UpdateCategory(string category)
        {
            if (SelectedTransaction != null)
            {
                _db.UpdateTransactionCategory(SelectedTransaction.Id, category);
                _categorization.LearnFromUserCorrection(SelectedTransaction.Description, category);
                LoadTransactions();
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadTransactions();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            LoadTransactions();
        }
    }
}
