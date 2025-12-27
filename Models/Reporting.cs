using System;
using System.Collections.Generic;

namespace FinDesk.Models
{
    /// <summary>
    /// Типи звітів, що підтримуються системою.
    /// </summary>
    public enum ReportType
    {
        MonthlyIncomeExpense,
        CategoryBreakdown,
        BudgetPerformance,
        YearEndSummary,
        CustomRange
    }

    /// <summary>
    /// Формати експорту звітів.
    /// </summary>
    public enum ExportFormat
    {
        Csv,
        Excel,
        Pdf
    }

    /// <summary>
    /// Налаштування вибору полів та фільтрів при експорті.
    /// </summary>
    public class ExportOptions
    {
        public List<string> SelectedColumns { get; set; } = new();

        public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string Delimiter { get; set; } = ",";
    }

    /// <summary>
    /// Опис запиту на формування звіту.
    /// </summary>
    public class ReportRequest
    {
        public ReportType Type { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public string? Category { get; set; }

        public string? Account { get; set; }

        public List<string> Columns { get; set; } = new();

        public ExportFormat PreferredFormat { get; set; } = ExportFormat.Csv;
    }

    /// <summary>
    /// Дані рядка звіту у вигляді словника для гнучкого відбору колонок.
    /// </summary>
    public class ReportRow
    {
        public Dictionary<string, object> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);

        public object this[string key]
        {
            get => Columns[key];
            set => Columns[key] = value;
        }
    }

    /// <summary>
    /// Значення для побудови графіків/діаграм у звітах.
    /// </summary>
    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;

        public decimal Value { get; set; }
    }

    /// <summary>
    /// Набір даних для візуалізацій.
    /// </summary>
    public class ChartData
    {
        public string Title { get; set; } = string.Empty;

        public string Type { get; set; } = "bar";

        public List<ChartPoint> Points { get; } = new();
    }

    /// <summary>
    /// Результат сформованого звіту з агрегатами та візуалізаціями.
    /// </summary>
    public class ReportResult
    {
        public ReportType Type { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public Dictionary<string, decimal> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<ReportRow> Rows { get; } = new();

        public List<ChartData> Charts { get; } = new();
    }

    /// <summary>
    /// Представлення сторінки звіту після генерації шаблоном.
    /// </summary>
    public class ReportPage
    {
        public int PageNumber { get; set; }

        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Згенерований документ зі сторінками та графіками.
    /// </summary>
    public class ReportDocument
    {
        public string Title { get; set; } = string.Empty;

        public List<ReportPage> Pages { get; } = new();

        public List<ChartData> Charts { get; } = new();
    }
}

