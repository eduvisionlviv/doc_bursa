using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.ViewModels
{
    public partial class BudgetViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorizationService;
        private readonly BudgetService _budgetService;
        private readonly BudgetAnalyzer _analyzer;

        [ObservableProperty]
        private ObservableCollection<BudgetAnalysisResult> budgets = new();

        [ObservableProperty]
        private ObservableCollection<BudgetAlert> alerts = new();

        [ObservableProperty]
        private ObservableCollection<BudgetPeriodSummary> monthlySummaries = new();

        [ObservableProperty]
        private ObservableCollection<BudgetPeriodSummary> yearlySummaries = new();

        [ObservableProperty]
        private BudgetAnalysisResult? selectedBudget;

        [ObservableProperty]
        private ObservableCollection<string> categories = new();

        [ObservableProperty]
        private string newBudgetName = string.Empty;

        [ObservableProperty]
        private string newBudgetCategory = string.Empty;

        [ObservableProperty]
        private decimal newBudgetLimit = 1000m;

        [ObservableProperty]
        private int newBudgetAlertThreshold = 80;

        [ObservableProperty]
        private BudgetFrequency selectedFrequency = BudgetFrequency.Monthly;

        [ObservableProperty]
        private DateTime newBudgetStartDate = DateTime.UtcNow.Date;

        [ObservableProperty]
        private string newBudgetDescription = string.Empty;

        public Array Frequencies => Enum.GetValues(typeof(BudgetFrequency));

        public BudgetViewModel()
        {
            _databaseService = new DatabaseService();
            var deduplicationService = new DeduplicationService(_databaseService);
            _categorizationService = new CategorizationService(_databaseService);
            _transactionService = new TransactionService(_databaseService, deduplicationService);
            _budgetService = new BudgetService(_databaseService, _transactionService, _categorizationService);
            _analyzer = new BudgetAnalyzer(_transactionService, _categorizationService);

            LoadCategories();
            RefreshData();
        }

        [RelayCommand]
        private void RefreshData()
        {
            var analyses = _budgetService.EvaluateAllBudgets();
            Budgets = new ObservableCollection<BudgetAnalysisResult>(analyses.Values.OrderBy(b => b.Budget.Name));
            Alerts = new ObservableCollection<BudgetAlert>(_budgetService.GetAlerts());

            if (SelectedBudget != null)
            {
                UpdatePeriodSummaries(SelectedBudget.Budget);
            }
            else if (Budgets.Any())
            {
                SelectedBudget = Budgets.First();
            }
        }

        [RelayCommand]
        private void AddBudget()
        {
            if (string.IsNullOrWhiteSpace(NewBudgetName))
            {
                return;
            }

            var budget = new Budget
            {
                Name = NewBudgetName.Trim(),
                Category = NewBudgetCategory?.Trim() ?? string.Empty,
                Limit = NewBudgetLimit,
                AlertThreshold = NewBudgetAlertThreshold,
                Frequency = SelectedFrequency,
                StartDate = NewBudgetStartDate == default ? DateTime.UtcNow.Date : NewBudgetStartDate.Date,
                Description = NewBudgetDescription ?? string.Empty
            };

            _budgetService.CreateBudget(budget);
            RefreshData();
            ClearForm();
        }

        [RelayCommand]
        private void DeleteBudget(Guid budgetId)
        {
            _budgetService.DeleteBudget(budgetId);
            RefreshData();
        }

        [RelayCommand]
        private void RefreshCategories()
        {
            LoadCategories();
        }

        partial void OnSelectedBudgetChanged(BudgetAnalysisResult? value)
        {
            if (value != null)
            {
                UpdatePeriodSummaries(value.Budget);
            }
        }

        private void UpdatePeriodSummaries(Budget budget)
        {
            MonthlySummaries = new ObservableCollection<BudgetPeriodSummary>(_analyzer.GetMonthlyView(budget, DateTime.UtcNow.Year));
            YearlySummaries = new ObservableCollection<BudgetPeriodSummary>(_analyzer.GetYearlyView(budget, DateTime.UtcNow.Year - 1, 2));
        }

        private void LoadCategories()
        {
            var categoryList = _databaseService.GetTransactions()
                .Where(t => !string.IsNullOrWhiteSpace(t.Category))
                .Select(t => t.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            Categories = new ObservableCollection<string>(categoryList);
        }

        private void ClearForm()
        {
            NewBudgetName = string.Empty;
            NewBudgetCategory = string.Empty;
            NewBudgetLimit = 1000m;
            NewBudgetAlertThreshold = 80;
            NewBudgetDescription = string.Empty;
            SelectedFrequency = BudgetFrequency.Monthly;
            NewBudgetStartDate = DateTime.UtcNow.Date;
        }
    }
}
