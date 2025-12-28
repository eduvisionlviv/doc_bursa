using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace doc_bursa.ViewModels
{
    public enum ImportGroup { Api, File, Manual }

    public partial class SourcesViewModel : ViewModelBase
    {
        private readonly MonobankService _monobankService;
        private readonly PrivatBankService _privatBankService;

        [ObservableProperty]
        private ObservableCollection<AccountGroup> _accountGroups = new();

        [ObservableProperty]
        private ImportGroup _selectedImportGroup = ImportGroup.Api;

        [ObservableProperty]
        private bool _isMappingAccounts;

        [ObservableProperty]
        private DataSource _mappingSource = new();

        [ObservableProperty]
        private ObservableCollection<DiscoveredAccount> _discoveredAccounts = new();

        public SourcesViewModel(MonobankService mono, PrivatBankService privat)
        {
            _monobankService = mono;
            _privatBankService = privat;
        }

        [RelayCommand]
        private async Task DiscoverAccounts()
        {
            if (MappingSource == null || string.IsNullOrEmpty(MappingSource.Token)) return;

            if (MappingSource.Provider == "Monobank")
            {
                var accounts = await _monobankService.DiscoverAccountsAsync(MappingSource.Token);
                DiscoveredAccounts = new ObservableCollection<DiscoveredAccount>(accounts);
            }
            else if (MappingSource.Provider == "PrivatBank")
            {
                 // Логіка для Привату
            }
            
            IsMappingAccounts = DiscoveredAccounts.Count > 0;
        }
    }
}
