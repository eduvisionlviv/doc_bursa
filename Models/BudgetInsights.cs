using System;

namespace FinDesk.Models
{
    /// <summary>
    /// Результат аналізу бюджету з метриками використання.
    /// </summary>
    public class BudgetAnalysisResult
    {
        public Budget Budget { get; set; } = new Budget();
        public decimal ActualSpent { get; set; }
        public decimal Remaining { get; set; }
        public decimal UsagePercentage { get; set; }
        public bool IsOverBudget { get; set; }
        public bool ShouldAlert { get; set; }
        public BudgetForecast Forecast { get; set; } = new BudgetForecast();
    }

    /// <summary>
    /// Прогноз на кінець періоду за поточним темпом витрат.
    /// </summary>
    public class BudgetForecast
    {
        public decimal ProjectedAmount { get; set; }
        public decimal ProjectedUsagePercentage { get; set; }
    }

    /// <summary>
    /// Алерт бюджету.
    /// </summary>
    public class BudgetAlert
    {
        public Guid BudgetId { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal UsagePercentage { get; set; }
        public decimal Limit { get; set; }
        public decimal Spent { get; set; }
        public bool IsOverBudget { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Представлення витрат за період.
    /// </summary>
    public class BudgetPeriodSummary
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public decimal Spent { get; set; }
        public decimal UsagePercentage { get; set; }
    }
}
