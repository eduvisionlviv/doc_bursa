#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace doc_bursa.ViewModels
{
    /// <summary>
    /// Простий, самодостатній Budget VM для WPF/MVVM.
    /// Не має залежностей від інших класів проєкту, тому стабільно збирається.
    /// </summary>
    public class BudgetViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<BudgetItem> BudgetItems { get; } = new ObservableCollection<BudgetItem>();

        // Часто у вьюх/байндінгах трапляються альтернативні назви
        public ObservableCollection<BudgetItem> Items => BudgetItems;

        private BudgetItem? _selectedBudgetItem;
        public BudgetItem? SelectedBudgetItem
        {
            get => _selectedBudgetItem;
            set
            {
                if (SetProperty(ref _selectedBudgetItem, value))
                {
                    _removeSelectedCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newItemName = string.Empty;
        public string NewItemName
        {
            get => _newItemName;
            set
            {
                if (SetProperty(ref _newItemName, value))
                    _addItemCommand.RaiseCanExecuteChanged();
            }
        }

        private decimal _newItemPlanned;
        public decimal NewItemPlanned
        {
            get => _newItemPlanned;
            set
            {
                if (SetProperty(ref _newItemPlanned, value))
                    _addItemCommand.RaiseCanExecuteChanged();
            }
        }

        private decimal _newItemActual;
        public decimal NewItemActual
        {
            get => _newItemActual;
            set
            {
                if (SetProperty(ref _newItemActual, value))
                    _addItemCommand.RaiseCanExecuteChanged();
            }
        }

        public decimal TotalPlanned => BudgetItems.Sum(i => i.Planned);
        public decimal TotalActual  => BudgetItems.Sum(i => i.Actual);
        public decimal Remaining    => TotalPlanned - TotalActual;

        private readonly RelayCommand _addItemCommand;
        private readonly RelayCommand _removeSelectedCommand;
        private readonly RelayCommand _clearActualsCommand;
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _loadCommand;

        public ICommand AddItemCommand => _addItemCommand;
        public ICommand RemoveSelectedCommand => _removeSelectedCommand;
        public ICommand ClearActualsCommand => _clearActualsCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand LoadCommand => _loadCommand;

        // Часті "звичні" назви команд
        public ICommand AddBudgetItemCommand => _addItemCommand;
        public ICommand RemoveBudgetItemCommand => _removeSelectedCommand;

        /// <summary>
        /// Один конструктор "на все" — підходить і для DI, і для new BudgetViewModel().
        /// </summary>
        public BudgetViewModel(params object[]? _)
        {
            _addItemCommand = new RelayCommand(AddItem, CanAddItem);
            _removeSelectedCommand = new RelayCommand(RemoveSelected, () => SelectedBudgetItem != null);
            _clearActualsCommand = new RelayCommand(ClearActuals, () => BudgetItems.Count > 0);

            // Заглушки під інтеграцію зі збереженням/завантаженням, щоб збірка проходила стабільно
            _saveCommand = new RelayCommand(() => { /* TODO: підключити persistence */ });
            _loadCommand = new RelayCommand(() => { /* TODO: підключити persistence */ });

            BudgetItems.CollectionChanged += BudgetItems_CollectionChanged;
        }

        private void BudgetItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (BudgetItem item in e.OldItems)
                    item.PropertyChanged -= BudgetItem_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (BudgetItem item in e.NewItems)
                    item.PropertyChanged += BudgetItem_PropertyChanged;
            }

            RaiseTotals();
            _clearActualsCommand.RaiseCanExecuteChanged();
        }

        private void BudgetItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BudgetItem.Planned) || e.PropertyName == nameof(BudgetItem.Actual))
                RaiseTotals();
        }

        private bool CanAddItem()
        {
            if (string.IsNullOrWhiteSpace(NewItemName)) return false;
            if (NewItemPlanned < 0) return false;
            if (NewItemActual < 0) return false;
            return true;
        }

        private void AddItem()
        {
            var item = new BudgetItem
            {
                Name = NewItemName.Trim(),
                Planned = NewItemPlanned,
                Actual = NewItemActual
            };

            BudgetItems.Add(item);
            SelectedBudgetItem = item;

            // reset input
            NewItemName = string.Empty;
            NewItemPlanned = 0;
            NewItemActual = 0;

            _addItemCommand.RaiseCanExecuteChanged();
        }

        private void RemoveSelected()
        {
            var item = SelectedBudgetItem;
            if (item == null) return;

            BudgetItems.Remove(item);
            SelectedBudgetItem = BudgetItems.LastOrDefault();
        }

        private void ClearActuals()
        {
            foreach (var i in BudgetItems)
                i.Actual = 0;

            RaiseTotals();
        }

        private void RaiseTotals()
        {
            OnPropertyChanged(nameof(TotalPlanned));
            OnPropertyChanged(nameof(TotalActual));
            OnPropertyChanged(nameof(Remaining));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public sealed class BudgetItem : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            private decimal _planned;
            public decimal Planned
            {
                get => _planned;
                set => SetProperty(ref _planned, value);
            }

            private decimal _actual;
            public decimal Actual
            {
                get => _actual;
                set => SetProperty(ref _actual, value);
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
            {
                if (Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public RelayCommand(Action execute, Func<bool>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute ?? (() => true);
            }

            public bool CanExecute(object? parameter) => _canExecute();

            public void Execute(object? parameter) => _execute();

            public event EventHandler? CanExecuteChanged;

            public void RaiseCanExecuteChanged() =>
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

// Дублюючий namespace на випадок, якщо частина проєкту вже перейменована під FinDesk.*
namespace FinDesk.ViewModels
{
    public class BudgetViewModel : doc_bursa.ViewModels.BudgetViewModel
    {
        public BudgetViewModel(params object[]? services) : base(services ?? Array.Empty<object>()) { }
    }
}
