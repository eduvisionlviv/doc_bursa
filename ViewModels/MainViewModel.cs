using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Views;

namespace FinDesk.ViewModels
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
                                "Groups" => new GroupsView(),
                _ => CurrentView
            };
        }
    }
}

