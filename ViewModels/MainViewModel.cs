using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;
using doc_bursa.Views;

namespace doc_bursa.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private object? currentView;

        [ObservableProperty]
        private MasterGroup? selectedMasterGroup;

        public ObservableCollection<MasterGroup> MasterGroups { get; } = new();

        // ViewModels
        public DashboardViewModel DashboardViewModel { get; }
        public TransactionsViewModel TransactionsViewModel { get; }
        public SourcesViewModel SourcesViewModel { get; }
        public BudgetViewModel BudgetViewModel { get; }
        public GroupsViewModel GroupsViewModel { get; }
        public AnalyticsViewModel AnalyticsViewModel { get; }

        public MainViewModel()
        {
            _databaseService = new DatabaseService();

            // Ініціалізуємо ViewModels
            DashboardViewModel = new DashboardViewModel(_databaseService);
            TransactionsViewModel = new TransactionsViewModel(_databaseService);
            SourcesViewModel = new SourcesViewModel();
            BudgetViewModel = new BudgetViewModel();
            GroupsViewModel = new GroupsViewModel(_databaseService);
            AnalyticsViewModel = new AnalyticsViewModel(_databaseService);

            LoadMasterGroups();

            // За замовчуванням показуємо Dashboard
            CurrentView = new DashboardView { DataContext = DashboardViewModel };
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            CurrentView = destination switch
            {
                "Dashboard" => new DashboardView { DataContext = DashboardViewModel },
                "Transactions" => new TransactionsView { DataContext = TransactionsViewModel },
                "Sources" => new SourcesView { DataContext = SourcesViewModel },
                "Budgets" => new BudgetView { DataContext = BudgetViewModel },
                "Groups" => new GroupsView { DataContext = GroupsViewModel },
                "Analytics" => new AnalyticsView { DataContext = AnalyticsViewModel },
                _ => CurrentView
            };
        }

        private void LoadMasterGroups()
        {
            MasterGroups.Clear();
            var groups = _databaseService.GetMasterGroups();
            foreach (var group in groups)
            {
                MasterGroups.Add(group);
            }

            SelectedMasterGroup ??= MasterGroups.FirstOrDefault();
            DashboardViewModel.UpdateMasterGroups(MasterGroups, SelectedMasterGroup);
            TransactionsViewModel.SelectedMasterGroup = SelectedMasterGroup;
            AnalyticsViewModel.SelectedMasterGroup = SelectedMasterGroup;
        }

        partial void OnSelectedMasterGroupChanged(MasterGroup? value)
        {
            DashboardViewModel.SelectedMasterGroup = value;
            TransactionsViewModel.SelectedMasterGroup = value;
            AnalyticsViewModel.SelectedMasterGroup = value;
        }
    }
}
