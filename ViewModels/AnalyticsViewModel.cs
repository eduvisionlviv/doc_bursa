using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FinDesk.ViewModels
{
    public class AnalyticsViewModel : ViewModelBase
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

        private string _selectedPeriod = string.Empty;
        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                SetProperty(ref _selectedPeriod, value);
                _ = LoadAnalyticsAsync();
            }
        }

        private ISeries[] _trendSeries = Array.Empty<ISeries>();
        public ISeries[] TrendSeries
        {
            get => _trendSeries;
            set => SetProperty(ref _trendSeries, value);
        }

        private Axis[] _trendAxes = new[] { new Axis { Labels = Array.Empty<string>() } };
        public Axis[] TrendAxes
        {
            get => _trendAxes;
            set => SetProperty(ref _trendAxes, value);
        }

        private ISeries[] _categorySeries = Array.Empty<ISeries>();
        public ISeries[] CategorySeries
        {
            get => _categorySeries;
            set => SetProperty(ref _categorySeries, value);
        }

        private ISeries[] _debitCreditSeries = Array.Empty<ISeries>();
        public ISeries[] DebitCreditSeries
        {
            get => _debitCreditSeries;
            set => SetProperty(ref _debitCreditSeries, value);
        }

        private Axis[] _debitCreditAxes = new[] { new Axis { Labels = new[] { "Дохід", "Витрати" } } };
        public Axis[] DebitCreditAxes
        {
            get => _debitCreditAxes;
            set => SetProperty(ref _debitCreditAxes, value);
        }

        private ISeries[] _cashFlowSeries = Array.Empty<ISeries>();
        public ISeries[] CashFlowSeries
        {
            get => _cashFlowSeries;
            set => SetProperty(ref _cashFlowSeries, value);
        }

        private Axis[] _cashFlowAxes = new[] { new Axis { Labels = Array.Empty<string>() } };
        public Axis[] CashFlowAxes
        {
            get => _cashFlowAxes;
            set => SetProperty(ref _cashFlowAxes, value);
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
            _analytics = new AnalyticsService(_db);

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

                BuildSeries(filtered);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analytics Load Error: {ex.Message}");
            }
        }

        private void BuildSeries(List<Transaction> filtered)
        {
            var monthly = filtered
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Label = $"{g.Key.Month:D2}.{g.Key.Year}",
                    Balance = g.Sum(t => t.Amount)
                })
                .ToList();

            TrendAxes = new[]
            {
                new Axis { Labels = monthly.Select(m => m.Label).ToArray(), LabelsRotation = 15 }
            };

            TrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Баланс",
                    Values = monthly.Select(m => (double)m.Balance).ToArray(),
                    GeometrySize = 8,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 2 }
                }
            };

            CategorySeries = TopCategories
                .Select(cat => new PieSeries<double>
                {
                    Name = cat.Name ?? "Невідомо",
                    Values = new[] { Math.Abs((double)cat.Amount) },
                    DataLabelsSize = 12,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black)
                })
                .Cast<ISeries>()
                .ToArray();

            DebitCreditSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "Дохід",
                    Values = new[] { (double)TotalIncome },
                    Fill = new SolidColorPaint(SKColors.SeaGreen)
                },
                new ColumnSeries<double>
                {
                    Name = "Витрати",
                    Values = new[] { (double)TotalExpenses },
                    Fill = new SolidColorPaint(SKColors.OrangeRed)
                }
            };

            var daily = filtered
                .GroupBy(t => t.TransactionDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Label = g.Key.ToString("dd.MM"),
                    Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    Expense = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount))
                })
                .ToList();

            CashFlowAxes = new[]
            {
                new Axis { Labels = daily.Select(d => d.Label).ToArray(), LabelsRotation = 30 }
            };

            CashFlowSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Дохід",
                    Values = daily.Select(d => (double)d.Income).ToArray(),
                    GeometrySize = 6,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.SeaGreen) { StrokeThickness = 2 }
                },
                new LineSeries<double>
                {
                    Name = "Витрати",
                    Values = daily.Select(d => (double)d.Expense).ToArray(),
                    GeometrySize = 6,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 2 }
                }
            };
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
