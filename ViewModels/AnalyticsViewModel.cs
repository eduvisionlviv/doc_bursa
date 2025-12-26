using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.ViewModels
{
using doc_bursa.Models;
    public class AnalyticsViewModel : ViewModelBase
    using doc_bursa.Services;
    {
        private readonly DatabaseService _db;
        private readonly AnalyticsService _analytics;

        // Властивості для статистики
        private decimal _totalIncome;
        public decimal TotalIncome
        {
            get => _totalIncome;
            set => SetProperty(ref _totalIncome, value);
        }

        private decimal _totalExpenses;
        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        private decimal _netCashFlow;
        public decimal NetCashFlow
        {
            get => _netCashFlow;
            set => SetProperty(ref _netCashFlow, value);
        }

        private decimal _averageDaily;
        public decimal AverageDaily
        {
            get => _averageDaily;
            set => SetProperty(ref _averageDaily, value);
        }

        private string _selectedPeriod;
        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                SetProperty(ref _selectedPeriod, value);
                _ = LoadAnalyticsAsync();
            }
        }

        // Колекції
        public ObservableCollection<Category> TopCategories { get; set; }
        public ObservableCollection<string> Periods { get; set; }

        // Команди
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

        public AnalyticsViewModel()
        {
            _db = new DatabaseService();
            _analytics = new AnalyticsService();

            TopCategories = new ObservableCollection<Category>();
            Periods = new ObservableCollection<string>
            {
                "Цього місяця",
                "Минулого місяця",
                "Останні 3 місяці",
                "Останні 6 місяців",
                "Цього року"
            };

            SelectedPeriod = Periods[0];

            RefreshCommand = new RelayCommand(async () => await LoadAnalyticsAsync());
            ExportCommand = new RelayCommand(async () => await ExportAnalyticsAsync());

            _ = LoadAnalyticsAsync();
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                var (startDate, endDate) = GetDateRange(SelectedPeriod);
                var transactions = await _db.GetTransactionsAsync();
                var filtered = transactions.Where(t => 
                    t.TransactionDate >= startDate && 
                    t.TransactionDate <= endDate).ToList();

                // Розрахунок статистики
                TotalIncome = filtered.Where(t => t.Amount > 0).Sum(t => t.Amount);
                TotalExpenses = Math.Abs(filtered.Where(t => t.Amount < 0).Sum(t => t.Amount));
                NetCashFlow = TotalIncome - TotalExpenses;

                var days = (endDate - startDate).Days + 1;
                AverageDaily = days > 0 ? NetCashFlow / days : 0;

                // Топ категорій
                var categoryStats = _analytics.GetCategoryBreakdown(filtered);
                TopCategories.Clear();
                foreach (var cat in categoryStats.OrderByDescending(c => Math.Abs(c.Value)).Take(10))
                {
                    var count = filtered.Count(t => t.Category == cat.Key);
                    TopCategories.Add(new Category 
                    { 
                        Name = cat.Key, 
                        Amount = cat.Value,
                        Count = count
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analytics Load Error: {ex.Message}");
            }
        }

        private (DateTime start, DateTime end) GetDateRange(string period)
        {
            var today = DateTime.Today;
            return period switch
            {
                "Цього місяця" => (new DateTime(today.Year, today.Month, 1), today),
                "Минулого місяця" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
                "Останні 3 місяці" => (today.AddMonths(-3), today),
                "Останні 6 місяців" => (today.AddMonths(-6), today),
                "Цього року" => (new DateTime(today.Year, 1, 1), today),
                _ => (today.AddMonths(-1), today)
            };
        }

        private async Task ExportAnalyticsAsync()
        {
            // TODO: Імплементувати експорт звіту
            await Task.CompletedTask;
        }
    }
}
