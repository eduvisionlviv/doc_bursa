using CommunityToolkit.Mvvm.ComponentModel;
using FinDesk.Models;
using FinDesk.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly AnalyticsService _analytics;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] private decimal income;
    [ObservableProperty] private decimal expense;
    [ObservableProperty] private decimal net;

    [ObservableProperty] private ISeries[] pieSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] lineSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] yAxes = Array.Empty<Axis>();

    public DashboardViewModel(AnalyticsService analytics, MainWindowViewModel shell)
    {
        _analytics = analytics;
        _shell = shell;
    }

    public async Task RefreshAsync(DateTime fromUtc, DateTime toUtc)
    {
        var cards = await _analytics.GetCardsAsync(fromUtc, toUtc);
        Income = cards.income;
        Expense = cards.expense;
        Net = cards.net;

        var byCat = await _analytics.ByCategoryAsync(fromUtc, toUtc);
        PieSeries = byCat
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => new PieSeries<decimal>
            {
                Values = new[] { kv.Value },
                Name = kv.Key.ToString()
            })
            .Cast<ISeries>()
            .ToArray();

        var byDay = await _analytics.ExpenseByDayAsync(fromUtc, toUtc);
        var xs = byDay.Keys.Select(d => d.ToString("MM-dd")).ToArray();
        var ys = byDay.Values.ToArray();

        LineSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Values = ys,
                Name = "Витрати/день",
                GeometrySize = 6
            }
        };

        XAxes = new[]
        {
            new Axis { Labels = xs }
        };
        YAxes = new[]
        {
            new Axis { }
        };
    }
}
