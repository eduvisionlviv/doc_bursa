using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;

namespace doc_bursa.ViewModels
{
    public partial class TransactionsViewModel : ObservableObject
    {
        private const int PageSize = 100;

        private readonly DatabaseService _db;
        private readonly CategorizationService _categorization;
        private readonly TransactionService _transactionService;
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

        [ObservableProperty]
        private bool isSplitWizardVisible;

        [ObservableProperty]
        private ObservableCollection<Transaction> splitChildren = new();

        [ObservableProperty]
        private string splitValidationMessage = string.Empty;

        public List<string> Categories { get; } = new()
        {
            "Всі", "Продукти", "Транспорт", "Ресторани", "Здоров'я", "Розваги", "Дохід", "Інше"
        };

        public TransactionsViewModel(DatabaseService? databaseService = null)
        {
            _db = databaseService ?? new DatabaseService();
            _categorization = new CategorizationService(_db);
            var deduplication = new DeduplicationService(_db);
            _transactionService = new TransactionService(_db, deduplication);
            LoadTransactions();
        }

        [RelayCommand]
        private void LoadTransactions()
        {
            var accountFilter = SelectedMasterGroup?.AccountNumbers ?? Array.Empty<string>();
            var allTransactions = _db.GetTransactions(accounts: accountFilter);

            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered.Where(MatchesSearch);
            }

            if (SelectedCategory != "Всі")
            {
                filtered = filtered.Where(MatchesCategory);
            }

            _filteredTransactions = filtered.ToList();
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

        [RelayCommand]
        private void StartSplitWizard()
        {
            if (SelectedTransaction == null)
            {
                return;
            }

            IsSplitWizardVisible = true;
            BuildSplitDraft();
        }

        [RelayCommand]
        private void AddSplitRow()
        {
            if (SelectedTransaction == null)
            {
                return;
            }

            var child = _transactionService.CreateChildTransaction(
                SelectedTransaction,
                0,
                $"{SelectedTransaction.Description} (частина)",
                SelectedTransaction.Category,
                SelectedTransaction.Account);

            SplitChildren.Add(child);
        }

        [RelayCommand]
        private void RemoveSplitRow(Transaction? child)
        {
            if (child == null)
            {
                return;
            }

            SplitChildren.Remove(child);
        }

        [RelayCommand]
        private void SaveSplit()
        {
            if (SelectedTransaction == null)
            {
                return;
            }

            if (!_transactionService.ValidateSplitTotals(SelectedTransaction, SplitChildren, out var diff))
            {
                SplitValidationMessage = $"Сума дочірніх не відповідає батьківській (різниця {diff:N2}).";
                return;
            }

            try
            {
                _transactionService.ApplySplit(SelectedTransaction, SplitChildren);
                SplitValidationMessage = string.Empty;
                IsSplitWizardVisible = false;
                LoadTransactions();
            }
            catch (Exception ex)
            {
                SplitValidationMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CancelSplitWizard()
        {
            IsSplitWizardVisible = false;
            SplitChildren.Clear();
            SplitValidationMessage = string.Empty;
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

        partial void OnSelectedTransactionChanged(Transaction? value)
        {
            if (value != null && IsSplitWizardVisible)
            {
                BuildSplitDraft();
            }
        }

        private void BuildSplitDraft()
        {
            SplitChildren.Clear();
            SplitValidationMessage = string.Empty;

            if (SelectedTransaction == null)
            {
                return;
            }

            var source = SelectedTransaction.Children.Any()
                ? SelectedTransaction.Children
                : new ObservableCollection<Transaction> { _transactionService.CreateChildTransaction(SelectedTransaction, SelectedTransaction.Amount, SelectedTransaction.Description, SelectedTransaction.Category, SelectedTransaction.Account) };

            foreach (var child in source)
            {
                SplitChildren.Add(new Transaction
                {
                    TransactionId = child.TransactionId,
                    ParentTransactionId = child.ParentTransactionId,
                    Date = child.Date,
                    Amount = child.Amount,
                    Description = child.Description,
                    Category = child.Category,
                    Source = child.Source,
                    Counterparty = child.Counterparty,
                    Account = child.Account,
                    Balance = child.Balance,
                    Hash = child.Hash,
                    IsDuplicate = child.IsDuplicate,
                    OriginalTransactionId = child.OriginalTransactionId,
                    IsSplit = child.IsSplit
                });
            }
        }

        private bool MatchesSearch(Transaction transaction)
        {
            return transaction.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                   || transaction.Children.Any(MatchesSearch);
        }

        private bool MatchesCategory(Transaction transaction)
        {
            if (SelectedCategory == "Всі")
            {
                return true;
            }

            return string.Equals(transaction.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)
                   || transaction.Children.Any(MatchesCategory);
        }
    }
}
