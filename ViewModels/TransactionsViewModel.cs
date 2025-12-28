using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace doc_bursa.ViewModels
{
    public partial class TransactionsViewModel : ViewModelBase
    {
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categoryRepository;
        private List<Transaction> _allTransactions = new();

        [ObservableProperty]
        private ObservableCollection<Transaction> _transactions;

        [ObservableProperty]
        private MasterGroup _selectedMasterGroup;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddMonths(-1);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        public TransactionsViewModel(TransactionService transactionService, CategorizationService categoryRepository)
        {
            _transactionService = transactionService;
            _categoryRepository = categoryRepository;
            Transactions = new ObservableCollection<Transaction>();
        }

        [RelayCommand]
        private async Task LoadTransactions()
        {
            if (SelectedMasterGroup == null) return;

            // Завантажуємо транзакції
            _allTransactions = new List<Transaction>(); 
            
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<Transaction> query = _allTransactions;

            query = query.Where(t => t.Date >= StartDate && t.Date <= EndDate);

            if (SelectedMasterGroup != null)
            {
                // Фільтрація по MasterGroup
                query = query.Where(t => t.Account?.AccountGroup?.MasterGroupId == SelectedMasterGroup.Id);
            }

            Transactions = new ObservableCollection<Transaction>(query);
        }

        [RelayCommand]
        private async Task SplitTransaction(Transaction transaction)
        {
             await Task.CompletedTask;
        }
    }
}
