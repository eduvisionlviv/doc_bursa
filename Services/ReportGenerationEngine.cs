using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinDesk.Models;

namespace FinDesk.Services
{
    /// <summary>
    /// Шаблонний генератор звітів із підтримкою багатосторінковості та простих графіків.
    /// </summary>
    public class ReportGenerationEngine
    {
        private readonly Dictionary<ReportType, string> _templates = new()
        {
            { ReportType.MonthlyIncomeExpense, "Звіт: {Title}\\nПеріод: {Period}\\n" },
            { ReportType.CategoryBreakdown, "Категорії витрат\\nПеріод: {Period}\\n" },
            { ReportType.BudgetPerformance, "Бюджети та виконання\\nПеріод: {Period}\\n" },
            { ReportType.YearEndSummary, "Підсумки року\\nПеріод: {Period}\\n" },
            { ReportType.CustomRange, "Довільний звіт\\nПеріод: {Period}\\n" }
        };

        public ReportDocument BuildDocument(ReportResult report)
        {
            var document = new ReportDocument
            {
                Title = report.Title
            };

            var pageSize = 20;
            var pages = SplitIntoPages(report.Rows, pageSize);
            for (var i = 0; i < pages.Count; i++)
            {
                var pageRows = pages[i];
                var builder = new StringBuilder();
                builder.AppendLine(RenderHeader(report));
                builder.AppendLine($"Сторінка {i + 1}/{pages.Count}");
                builder.AppendLine(new string('-', 40));
                var columns = pageRows.FirstOrDefault()?.Columns.Keys.ToList() ?? new List<string>();
                if (columns.Count > 0)
                {
                    builder.AppendLine(string.Join(" | ", columns));
                    builder.AppendLine(new string('-', 40));
                }

                foreach (var row in pageRows)
                {
                    var values = columns.Select(c => row.Columns.TryGetValue(c, out var value) ? value?.ToString() ?? string.Empty : string.Empty);
                    builder.AppendLine(string.Join(" | ", values));
                }

                document.Pages.Add(new ReportPage
                {
                    PageNumber = i + 1,
                    Content = builder.ToString()
                });
            }

            foreach (var chart in report.Charts)
            {
                document.Charts.Add(chart);
            }

            return document;
        }

        private string RenderHeader(ReportResult report)
        {
            var template = _templates.TryGetValue(report.Type, out var value) ? value : "Звіт\\nПеріод: {Period}\\n";
            var period = $"{report.From:yyyy-MM-dd} - {report.To:yyyy-MM-dd}";
            return template
                .Replace("{Title}", report.Title)
                .Replace("{Period}", period);
        }

        private List<List<ReportRow>> SplitIntoPages(List<ReportRow> rows, int pageSize)
        {
            var pages = new List<List<ReportRow>>();
            for (var i = 0; i < rows.Count; i += pageSize)
            {
                pages.Add(rows.Skip(i).Take(pageSize).ToList());
            }

            if (pages.Count == 0)
            {
                pages.Add(new List<ReportRow>());
            }

            return pages;
        }
    }
}

