using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FinDesk.Models;

namespace FinDesk.Services
{
    /// <summary>
    /// Сервіс формування агрегованих звітів по транзакціях та бюджетах.
    /// </summary>
    public class ReportService
    {
        private readonly TransactionService _transactionService;
        private readonly BudgetService _budgetService;
        private readonly CategorizationService _categorizationService;

        public ReportService(TransactionService transactionService, BudgetService budgetService, CategorizationService categorizationService)
        {
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
            _categorizationService = categorizationService ?? throw new ArgumentNullException(nameof(categorizationService));
        }

        public ReportResult GenerateReport(ReportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.Type switch
            {
                ReportType.MonthlyIncomeExpense => BuildMonthlyIncomeExpense(request),
                ReportType.CategoryBreakdown => BuildCategoryBreakdown(request),
                ReportType.BudgetPerformance => BuildBudgetPerformance(request),
                ReportType.YearEndSummary => BuildYearEndSummary(request),
                ReportType.CustomRange => BuildCustomRange(request),
                _ => throw new NotSupportedException($"Невідомий тип звіту: {request.Type}")
            };
        }

        private ReportResult BuildMonthlyIncomeExpense(ReportRequest request)
        {
            var (from, to) = NormalizeRange(request.From, request.To, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            var transactions = FilterTransactions(from, to, request.Category, request.Account);

            var rows = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Label = $"{g.Key.Year}-{g.Key.Month:00}",
                    Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    Expense = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                    Balance = g.Sum(t => t.Amount)
                })
                .ToList();

            var result = new ReportResult
            {
                Type = ReportType.MonthlyIncomeExpense,
                Title = "Місячний звіт доходів/витрат",
                From = from,
                To = to
            };

            foreach (var row in rows)
            {
                var reportRow = new ReportRow();
                reportRow["Місяць"] = row.Label;
                reportRow["Дохід"] = row.Income;
                reportRow["Витрати"] = row.Expense;
                reportRow["Баланс"] = row.Balance;
                result.Rows.Add(reportRow);
            }

            result.Metrics["Загальний дохід"] = rows.Sum(r => r.Income);
            result.Metrics["Загальні витрати"] = rows.Sum(r => r.Expense);
            result.Metrics["Сальдо"] = rows.Sum(r => r.Balance);

            var chart = new ChartData { Title = "Доходи та витрати", Type = "column" };
            foreach (var row in rows)
            {
                chart.Points.Add(new ChartPoint { Label = row.Label + " (Д)", Value = row.Income });
                chart.Points.Add(new ChartPoint { Label = row.Label + " (В)", Value = row.Expense });
            }

            result.Charts.Add(chart);
            return result;
        }

        private ReportResult BuildCategoryBreakdown(ReportRequest request)
        {
            var (from, to) = NormalizeRange(request.From, request.To, DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
            var transactions = FilterTransactions(from, to, request.Category, request.Account)
                .Where(t => t.Amount < 0)
                .ToList();

            var grouped = transactions
                .GroupBy(t => t.Category ?? "Невідомо")
                .Select(g => new { Category = g.Key, Total = Math.Abs(g.Sum(t => t.Amount)), Count = g.Count() })
                .OrderByDescending(g => g.Total)
                .ToList();

            var result = new ReportResult
            {
                Type = ReportType.CategoryBreakdown,
                Title = "Структура витрат за категоріями",
                From = from,
                To = to
            };

            foreach (var group in grouped)
            {
                var row = new ReportRow();
                row["Категорія"] = group.Category;
                row["Сума"] = group.Total;
                row["Кількість"] = group.Count;
                result.Rows.Add(row);
            }

            result.Metrics["Загальні витрати"] = grouped.Sum(g => g.Total);

            var chart = new ChartData { Title = "Категорії", Type = "pie" };
            foreach (var group in grouped)
            {
                chart.Points.Add(new ChartPoint { Label = group.Category, Value = group.Total });
            }

            result.Charts.Add(chart);
            return result;
        }

        private ReportResult BuildBudgetPerformance(ReportRequest request)
        {
            var budgets = _budgetService.GetBudgets().Where(b => b.IsActive).ToList();
            var result = new ReportResult
            {
                Type = ReportType.BudgetPerformance,
                Title = "Ефективність бюджетів",
                From = request.From,
                To = request.To
            };

            foreach (var budget in budgets)
            {
                var analysis = _budgetService.EvaluateBudget(budget, request.From, request.To, request.To ?? DateTime.UtcNow);
                var row = new ReportRow();
                row["Бюджет"] = budget.Name;
                row["Категорія"] = budget.Category;
                row["Ліміт"] = budget.Limit;
                row["Витрачено"] = analysis.ActualSpent;
                row["Залишок"] = budget.Limit - analysis.ActualSpent;
                row["% використання"] = analysis.UsagePercentage;
                result.Rows.Add(row);
            }

            result.Metrics["Кількість бюджетів"] = budgets.Count;
            result.Metrics["Середнє використання"] = budgets.Count == 0 ? 0 : Math.Round(result.Rows.Average(r => Convert.ToDecimal(r["% використання"], CultureInfo.InvariantCulture)), 2);

            var chart = new ChartData { Title = "Використання бюджету", Type = "bar" };
            foreach (var row in result.Rows)
            {
                chart.Points.Add(new ChartPoint { Label = row["Бюджет"].ToString() ?? string.Empty, Value = Convert.ToDecimal(row["% використання"], CultureInfo.InvariantCulture) });
            }

            result.Charts.Add(chart);
            return result;
        }

        private ReportResult BuildYearEndSummary(ReportRequest request)
        {
            var now = DateTime.UtcNow;
            var yearStart = new DateTime(now.Year, 1, 1);
            var yearEnd = new DateTime(now.Year, 12, 31);
            var transactions = FilterTransactions(yearStart, yearEnd, request.Category, request.Account);

            var totalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var totalExpense = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));
            var months = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new { g.Key.Month, Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount), Expense = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)) })
                .OrderBy(g => g.Month)
                .ToList();

            var result = new ReportResult
            {
                Type = ReportType.YearEndSummary,
                Title = $"Підсумки року {now.Year}",
                From = yearStart,
                To = yearEnd
            };

            result.Metrics["Дохід"] = totalIncome;
            result.Metrics["Витрати"] = totalExpense;
            result.Metrics["Баланс"] = totalIncome - totalExpense;

            foreach (var month in months)
            {
                var row = new ReportRow();
                row["Місяць"] = month.Month;
                row["Дохід"] = month.Income;
                row["Витрати"] = month.Expense;
                result.Rows.Add(row);
            }

            var chart = new ChartData { Title = "Баланс по місяцях", Type = "line" };
            foreach (var month in months)
            {
                chart.Points.Add(new ChartPoint { Label = month.Month.ToString("00"), Value = month.Income - month.Expense });
            }

            result.Charts.Add(chart);
            return result;
        }

        private ReportResult BuildCustomRange(ReportRequest request)
        {
            var (from, to) = NormalizeRange(request.From, request.To, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            var transactions = FilterTransactions(from, to, request.Category, request.Account);

            var result = new ReportResult
            {
                Type = ReportType.CustomRange,
                Title = "Звіт за довільний період",
                From = from,
                To = to
            };

            foreach (var transaction in transactions)
            {
                var row = new ReportRow();
                row["Дата"] = transaction.Date;
                row["Опис"] = transaction.Description;
                row["Сума"] = transaction.Amount;
                row["Категорія"] = transaction.Category;
                row["Рахунок"] = transaction.Account;
                result.Rows.Add(row);
            }

            result.Metrics["Кількість"] = transactions.Count;
            result.Metrics["Сума"] = transactions.Sum(t => t.Amount);
            return result;
        }

        private (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to, DateTime defaultFrom, DateTime defaultTo)
        {
            var start = from ?? defaultFrom;
            var end = to ?? defaultTo;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return (start, end);
        }

        private List<Transaction> FilterTransactions(DateTime? from, DateTime? to, string? category, string? account)
        {
            var transactions = _transactionService.GetTransactions();

            if (from.HasValue)
            {
                transactions = transactions.Where(t => t.Date >= from.Value).ToList();
            }

            if (to.HasValue)
            {
                transactions = transactions.Where(t => t.Date <= to.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                transactions = transactions.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(account))
            {
                transactions = transactions.Where(t => string.Equals(t.Account, account, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return transactions;
        }
    }
}
