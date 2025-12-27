using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using doc_bursa.Models;
using doc_bursa.Services;

namespace doc_bursa.ViewModels
{
    /// <summary>
    /// ViewModel для управління майстер групами рахунків
    /// </summary>
    public class GroupsViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private MasterGroup _selectedGroup;
        private string _newGroupName;
        private bool _isLoading;
        private string _selectedAccount;
        private string _selectedGroupAccount;

        public ObservableCollection<MasterGroup> Groups { get; set; }
        public ObservableCollection<string> AvailableAccounts { get; set; }
        public string SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (_selectedAccount != value)
                {
                    _selectedAccount = value;
                    OnPropertyChanged(nameof(SelectedAccount));
                }
            }
        }

        public string SelectedGroupAccount
        {
            get => _selectedGroupAccount;
            set
            {
                if (_selectedGroupAccount != value)
                {
                    _selectedGroupAccount = value;
                    OnPropertyChanged(nameof(SelectedGroupAccount));
                }
            }
        }

        public MasterGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != value)
                {
                    _selectedGroup = value;
                    OnPropertyChanged(nameof(SelectedGroup));
                    LoadGroupDetails();
                }
            }
        }

        public string NewGroupName
        {
            get => _newGroupName;
            set
            {
                if (_newGroupName != value)
                {
                    _newGroupName = value;
                    OnPropertyChanged(nameof(NewGroupName));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        public ICommand CreateGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand AddAccountToGroupCommand { get; }
        public ICommand RemoveAccountFromGroupCommand { get; }
        public ICommand SaveGroupCommand { get; }

        public GroupsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            
            Groups = new ObservableCollection<MasterGroup>();
            AvailableAccounts = new ObservableCollection<string>();
            SelectedAccount = string.Empty;
            SelectedGroupAccount = string.Empty;

            CreateGroupCommand = new RelayCommand(async () => await CreateGroupAsync(), () => !string.IsNullOrWhiteSpace(NewGroupName));
            DeleteGroupCommand = new RelayCommand(async () => await DeleteGroupAsync(), () => SelectedGroup != null);
            AddAccountToGroupCommand = new RelayCommand(async () => await AddAccountToGroupAsync());
            RemoveAccountFromGroupCommand = new RelayCommand(async () => await RemoveAccountFromGroupAsync(SelectedGroupAccount));
            SaveGroupCommand = new RelayCommand(async () => await SaveGroupAsync(), () => SelectedGroup != null);

            _ = LoadGroupsAsync();
            _ = LoadAvailableAccountsAsync();
        }

        public GroupsViewModel() : this(new DatabaseService())
        {
        }

        private async Task LoadGroupsAsync()
        {
            IsLoading = true;
            try
            {
                var groups = await _databaseService.GetMasterGroupsAsync();

                App.Current.Dispatcher.Invoke(() =>
                {
                    Groups.Clear();
                    foreach (var group in groups)
                    {
                        Groups.Add(group);
                    }

                    if (Groups.Any())
                    {
                        SelectedGroup = Groups.First();
                    }
                });
            }
            catch (Exception ex)
            {
                // TODO: Логування помилки
                System.Diagnostics.Debug.WriteLine($"Error loading groups: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAvailableAccountsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var accounts = _databaseService.GetUniqueAccounts();
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableAccounts.Clear();
                        foreach (var account in accounts)
                        {
                            AvailableAccounts.Add(account);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading accounts: {ex.Message}");
            }
        }

        private async Task CreateGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName))
                return;

            IsLoading = true;
            try
            {
                var newGroup = new MasterGroup
                {
                    Name = NewGroupName,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                await _databaseService.SaveMasterGroupAsync(newGroup);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Groups.Add(newGroup);
                    SelectedGroup = newGroup;
                    NewGroupName = string.Empty;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating group: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteGroupAsync()
        {
            if (SelectedGroup == null)
                return;

            IsLoading = true;
            try
            {
                var groupToDelete = SelectedGroup;
                await _databaseService.DeleteMasterGroupAsync(groupToDelete.Id);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Groups.Remove(groupToDelete);
                    SelectedGroup = Groups.FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting group: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddAccountToGroupAsync()
        {
            if (SelectedGroup == null || string.IsNullOrWhiteSpace(SelectedAccount))
                return;

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    SelectedGroup.AddAccount(SelectedAccount);
                });
            });
        }

        private async Task RemoveAccountFromGroupAsync(string accountNumber)
        {
            if (SelectedGroup == null || string.IsNullOrWhiteSpace(accountNumber))
                return;

            await Task.Run(() =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    SelectedGroup.RemoveAccount(accountNumber);
                });
            });
        }

        private async Task SaveGroupAsync()
        {
            if (SelectedGroup == null)
                return;

            IsLoading = true;
            try
            {
                await _databaseService.SaveMasterGroupAsync(SelectedGroup);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving group: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadGroupDetails()
        {
            if (SelectedGroup == null)
                return;

            // Завантажити детальну інформацію про групу
            // Обчислити TotalBalance, TotalDebit, TotalCredit
            Task.Run(() =>
            {
                try
                {
                    decimal totalDebit = 0;
                    decimal totalCredit = 0;

                    foreach (var accountNumber in SelectedGroup.AccountNumbers)
                    {
                        var transactions = _databaseService.GetTransactionsByAccount(accountNumber);
                        totalDebit += transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
                        totalCredit += transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedGroup.TotalDebit = totalDebit;
                        SelectedGroup.TotalCredit = totalCredit;
                        SelectedGroup.TotalBalance = totalDebit - totalCredit;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading group details: {ex.Message}");
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // RelayCommand helper class
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public async void Execute(object parameter)
        {
            await _executeAsync();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _executeAsync;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Func<T, Task> executeAsync, Func<T, bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        public async void Execute(object parameter)
        {
            await _executeAsync((T)parameter);
        }
    }
}
