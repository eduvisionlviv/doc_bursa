using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Views;

namespace FinDesk.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? currentView;

        // ViewModels
        public DashboardViewModel DashboardViewModel { get; }
        public TransactionsViewModel TransactionsViewModel { get; }
        public SourcesViewModel SourcesViewModel { get; }
        public BudgetViewModel BudgetViewModel { get; }
        public GroupsViewModel GroupsViewModel { get; }
        public AnalyticsViewModel AnalyticsViewModel { get; }

        public MainViewModel()
        {
            // Ініціалізуємо ViewModels
            DashboardViewModel = new DashboardViewModel();
            TransactionsViewModel = new TransactionsViewModel();
            SourcesViewModel = new SourcesViewModel();
            BudgetViewModel = new BudgetViewModel();
            GroupsViewModel = new GroupsViewModel();
            AnalyticsViewModel = new AnalyticsViewModel();

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
    }
}

