using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;

namespace doc_bursa.ViewModels
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
        private ObservableCollection<PlannedTransaction> plannedPayments = new();

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
            _transactionService = new TransactionService(_databaseService, deduplicationService, _categorizationService);
            _budgetService = new BudgetService(_databaseService);
