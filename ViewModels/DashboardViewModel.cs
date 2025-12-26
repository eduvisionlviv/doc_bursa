using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {        private readonly DatabaseService _db;
        [ObservableProperty]
        private decimal totalIncome;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private decimal balance;

        [ObservableProperty]
        private List<Category> categories = new();

        [ObservableProperty]
        private string selectedPeriod = "Поточний місяць";

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


