using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace doc_bursa.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly AnalyticsService _analyticsService;

        [ObservableProperty]
        private decimal totalIncome;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private decimal balance;

        [ObservableProperty]
        private List<Category> categories = new();

        [ObservableProperty]
        private ObservableCollection<Transaction> recentTransactions = new();

        [ObservableProperty]
        private string selectedPeriod = "Поточний місяць";

        [ObservableProperty]
        private MasterGroup? selectedMasterGroup;

        public ObservableCollection<MasterGroup> MasterGroups { get; } = new();

        // === ГРАФІК 1: ТРЕНД (ЛІНІЯ) ===
        private ISeries[] miniTrendSeries = Array.Empty<ISeries>();
        public ISeries[] MiniTrendSeries
        {
            get => miniTrendSeries;
            set => SetProperty(ref miniTrendSeries, value);
        }

        private Axis[] miniTrendAxes = Array.Empty<Axis>();
        public Axis[] MiniTrendAxes
        {
            get => miniTrendAxes;
            set => SetProperty(ref miniTrendAxes, value);
        }

        // === ГРАФІК 2: ВИТРАТИ (КРУГОВА ДІАГРАМА) - ДОДАНО ===
        private ISeries[] expensesPieSeries = Array.Empty<ISeries>();
        public ISeries[] ExpensesPieSeries
        {
            get => expensesPieSeries;
            set => SetProperty(ref expensesPieSeries, value);
        }

        private ISeries[] cashflowSeries = Array.Empty<ISeries>();
        public ISeries[] CashflowSeries
        {
            get => cashflowSeries;
            set => SetProperty(ref cashflowSeries, value);
        }

        private Axis[] cashflowAxes = Array.Empty<Axis>();
        public Axis[] CashflowAxes
        {
            get => cashflowAxes;
            set => SetProperty(ref cashflowAxes, value);
        }

        private ISeries[] structureSeries = Array.Empty<ISeries>();
        public ISeries[] StructureSeries
        {
            get => structureSeries;
            set => SetProperty(ref structureSeries, value);
        }

        private ISeries[] netWorthSeries = Array.Empty<ISeries>();
        public ISeries[] NetWorthSeries
        {
            get => netWorthSeries;
            set => SetProperty(ref netWorthSeries, value);
        }

        private Axis[] netWorthAxes = Array.Empty<Axis>();
        public Axis[] NetWorthAxes
        {
            get => netWorthAxes;
            set => SetProperty(ref netWorthAxes, value);
        }

        public List<string> Periods { get; } = new()
        {
            "Поточний місяць",
            "Минулий місяць",
            "Поточний рік",
            "Весь час"
        };

        public DashboardViewModel(DatabaseService? databaseService = null)
        {
            _db = databaseService ?? new DatabaseService();
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var (from, to) = GetDateRange();
            var accountFilter = SelectedMasterGroup?.AccountNumbers ?? Array.Empty<string>();
            var transactions = _db.GetTransactions(from, to, accounts: accountFilter);

            if (from.HasValue)
            {
                periodTransactions = periodTransactions.Where(t => t.Date >= from.Value);
            }

            if (to.HasValue)
            {
                periodTransactions = periodTransactions.Where(t => t.Date <= to.Value);
            }

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(periodTransactions.ToList(), out var pendingTransfers);
            InTransitTransfers = pendingTransfers;

            TotalIncome = operationalTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            TotalExpenses = Math.Abs(operationalTransactions.Where(t => t.Amount < 0).Sum(t => t.Amount));
            Balance = TotalIncome - TotalExpenses;
            PlannedExpenses = _analyticsService.GetPlannedExpenseTotal(from, to);
            FreeCash = Balance - PlannedExpenses;

            Categories = operationalTransactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => t.Category)
                .Select(g => new Category
                {
                    Name = g.Key,
                    Amount = Math.Abs(g.Sum(t => t.Amount)),
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var recent = operationalTransactions
                .OrderByDescending(t => t.Date)
                .Take(10)
                .ToList();
            RecentTransactions = new ObservableCollection<Transaction>(recent);

            // Будуємо обидва графіки
            BuildMiniTrend(operationalTransactions);
            BuildExpensesPieChart(operationalTransactions);
            BuildCashflowChart(operationalTransactions);
            BuildStructureChart(operationalTransactions);
            var netWorthTransactions = TransactionFilterHelper.FilterOperationalTransactions(scopedTransactions, out _);
            BuildNetWorthChart(netWorthTransactions);
        }

        private void BuildMiniTrend(List<Transaction> transactions)
        {
            var last7days = transactions
                .GroupBy(t => t.Date.Date)
                .OrderByDescending(g => g.Key)
                .Take(7)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Label = g.Key.ToString("dd.MM"),
                    Balance = g.Sum(t => t.Amount)
                })
                .ToList();

            MiniTrendAxes = new[]
            {
                new Axis { Labels = last7days.Select(x => x.Label).ToArray(), LabelsRotation = 20 }
            };

            MiniTrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = last7days.Select(x => (double)x.Balance).ToArray(),
                    Fill = null,
                    GeometrySize = 6,
                    Stroke = new SolidColorPaint(SKColors.MediumPurple) { StrokeThickness = 2 }
                }
            };
        }

        // === НОВИЙ МЕТОД ДЛЯ КРУГОВОЇ ДІАГРАМИ ===
        private void BuildExpensesPieChart(List<Transaction> transactions)
        {
            var expenses = transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Не визначено" : t.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Amount = Math.Abs(g.Sum(t => t.Amount))
                })
                .OrderByDescending(x => x.Amount)
                .Take(6) // Топ 6 категорій
                .ToList();

            if (!expenses.Any())
            {
                ExpensesPieSeries = Array.Empty<ISeries>();
                return;
            }

            ExpensesPieSeries = expenses.Select(x => new PieSeries<double>
            {
                Values = new double[] { (double)x.Amount },
                Name = x.Category,
                InnerRadius = 60, // Робить "пончик"
                Pushout = 2,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => $"{x.Category}"
            }).ToArray();
        }

        private void BuildCashflowChart(List<Transaction> transactions)
        {
            var monthly = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .TakeLast(6)
                .Select(g => new
                {
                    Label = $"{g.Key.Year}-{g.Key.Month:00}",
                    Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    Expense = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount))
                })
                .ToList();

            if (!monthly.Any())
            {
                CashflowSeries = Array.Empty<ISeries>();
                CashflowAxes = Array.Empty<Axis>();
                return;
            }

            CashflowAxes = new[]
            {
                new Axis { Labels = monthly.Select(x => x.Label).ToArray(), LabelsRotation = 15 }
            };

            CashflowSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "Дохід",
                    Values = monthly.Select(x => (double)x.Income).ToArray(),
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen) { StrokeThickness = 0 },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
                },
                new ColumnSeries<double>
                {
                    Name = "Витрати",
                    Values = monthly.Select(x => (double)x.Expense).ToArray(),
                    Fill = new SolidColorPaint(SKColors.IndianRed) { StrokeThickness = 0 },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
                }
            };
        }

        private void BuildStructureChart(List<Transaction> transactions)
        {
            var structure = transactions
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Не визначено" : t.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Amount = Math.Abs(g.Sum(t => t.Amount))
                })
                .OrderByDescending(x => x.Amount)
                .Take(8)
                .ToList();

            if (!structure.Any())
            {
                StructureSeries = Array.Empty<ISeries>();
                return;
            }

            StructureSeries = structure.Select(x => new PieSeries<double>
            {
                Values = new[] { (double)x.Amount },
                Name = x.Category,
                InnerRadius = 50,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            }).ToArray();
        }

        private void BuildNetWorthChart(List<Transaction> transactions)
        {
            var accountBalances = transactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Account))
                .GroupBy(t => t.Account)
                .Select(g =>
                {
                    var last = g.OrderByDescending(t => t.Date).FirstOrDefault();
                    var balanceValue = last != null && last.Balance != 0 ? last.Balance : g.Sum(t => t.Amount);
                    return new { Account = g.Key, Balance = balanceValue };
                })
                .OrderByDescending(x => x.Balance)
                .ToList();

            NetWorth = accountBalances.Sum(x => x.Balance);

            if (!accountBalances.Any())
            {
                NetWorthSeries = Array.Empty<ISeries>();
                NetWorthAxes = Array.Empty<Axis>();
                return;
            }

            NetWorthAxes = new[]
            {
                new Axis { Labels = accountBalances.Select(x => x.Account).ToArray(), LabelsRotation = 15 }
            };

            NetWorthSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "Баланс",
                    Values = accountBalances.Select(x => (double)x.Balance).ToArray(),
                    Fill = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 0 },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
                }
            };
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            LoadData();
        }

        partial void OnSelectedMasterGroupChanged(MasterGroup? value)
        {
            LoadData();
        }

        private (DateTime?, DateTime?) GetDateRange()
        {
            var now = DateTime.Now;

            return SelectedPeriod switch
            {
                "Поточний місяць" => (new DateTime(now.Year, now.Month, 1), now),
                "Минулий місяць" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1).AddDays(-1)),
                "Поточний рік" => (new DateTime(now.Year, 1, 1), now),
                _ => (null, null)
            };
        }

        public void UpdateMasterGroups(IEnumerable<MasterGroup> masterGroups, MasterGroup? currentSelection)
        {
            MasterGroups.Clear();
            foreach (var group in masterGroups)
            {
                MasterGroups.Add(group);
            }

            if (currentSelection != null)
            {
                SelectedMasterGroup = MasterGroups.FirstOrDefault(g => g.Id == currentSelection.Id) ?? currentSelection;
            }
            else if (SelectedMasterGroup == null)
            {
                SelectedMasterGroup = MasterGroups.FirstOrDefault();
            }
        }
    }
}
