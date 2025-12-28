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

        public List<string> Periods { get; } = new()
        {
            "Поточний місяць",
            "Минулий місяць",
            "Поточний рік",
            "Весь час"
        };

        public DashboardViewModel()
        {
            _db = new DatabaseService();
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var (from, to) = GetDateRange();
            var transactions = _db.GetTransactions(from, to);
            transactions = transactions.Where(t => !t.IsTransfer).ToList();

            TotalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            TotalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));
            Balance = TotalIncome - TotalExpenses;

            Categories = transactions
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

            var recent = transactions
                .OrderByDescending(t => t.Date)
                .Take(10)
                .ToList();
            RecentTransactions = new ObservableCollection<Transaction>(recent);

            // Будуємо обидва графіки
            BuildMiniTrend(transactions);
            BuildExpensesPieChart(transactions); 
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
                .GroupBy(t => t.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Amount = Math.Abs(g.Sum(t => t.Amount))
                })
                .OrderByDescending(x => x.Amount)
                .Take(6) // Топ 6 категорій
                .ToList();

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

        partial void OnSelectedPeriodChanged(string value)
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
    }
}
