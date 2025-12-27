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
        private const int PageSize = 100;

        private readonly DatabaseService _db;
        private readonly CategorizationService _categorization;
        private List<Transaction> _filteredTransactions = new();

        [ObservableProperty]
        private ObservableCollection<Transaction> transactions = new();

        [ObservableProperty]
        private Transaction? selectedTransaction;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedCategory = "Всі";

        [ObservableProperty]
        private int currentPage = 1;

        [ObservableProperty]
        private int totalPages = 1;

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

            _filteredTransactions = allTransactions;
            TotalPages = Math.Max(1, (int)Math.Ceiling(_filteredTransactions.Count / (double)PageSize));
            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            ApplyPaging();
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                ApplyPaging();
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                ApplyPaging();
            }
        }

        private void ApplyPaging()
        {
            var pageItems = _filteredTransactions
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            Transactions = new ObservableCollection<Transaction>(pageItems);
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
            CurrentPage = 1;
            LoadTransactions();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            CurrentPage = 1;
            LoadTransactions();
        }
    }
}

