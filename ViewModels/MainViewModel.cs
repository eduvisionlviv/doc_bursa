using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Views;

namespace doc_bursa.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? currentView;

        public MainViewModel()
        {
            CurrentView = new DashboardView();
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            CurrentView = viewName switch
            {
                "Dashboard" => new DashboardView(),
                "Transactions" => new TransactionsView(),
                "Sources" => new SourcesView(),
                "Budgets" => new BudgetView(),
                "Groups" => new GroupsView(),
                "Analytics" => new AnalyticsView(),
                _ => CurrentView
            };
        }
    }
}


