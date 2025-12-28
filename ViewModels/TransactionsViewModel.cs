using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using doc_bursa.Models;
using doc_bursa.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace doc_bursa.ViewModels
{
    public partial class TransactionsViewModel : ViewModelBase, IRecipient<MasterGroupChangedMessage>
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
            
            // Підписка на повідомлення про зміну групи
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(MasterGroupChangedMessage message)
        {
            SelectedMasterGroup = message.NewGroup;
            LoadTransactionsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadTransactions()
        {
            if (SelectedMasterGroup == null) return;

            // Завантажуємо транзакції (тут треба метод сервісу, що повертає транзакції)
            // Поки що заглушка, щоб код компілювався. 
            // В реальності: await _transactionService.GetByMasterGroupAsync(SelectedMasterGroup.Id);
            _allTransactions = new List<Transaction>(); 
            
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<Transaction> query = _allTransactions;

            query = query.Where(t => t.Date >= StartDate && t.Date <= EndDate);

            if (SelectedMasterGroup != null)
            {
                // Фільтрація по MasterGroup через навігаційні властивості
                // Перевірка на null для Account та AccountGroup важлива
                query = query.Where(t => t.Account?.AccountGroup?.MasterGroupId == SelectedMasterGroup.Id);
            }

            Transactions = new ObservableCollection<Transaction>(query);
        }

        // Заглушка для Split Command, щоб не було помилок в XAML
        [RelayCommand]
        private async Task SplitTransaction(Transaction transaction)
        {
             // Логіка спліту
             await Task.CompletedTask;
        }
    }
}
