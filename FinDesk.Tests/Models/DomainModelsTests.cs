using System;
using FinDesk.Models;
using Xunit;

namespace FinDesk.Tests.Models
{
    public class DomainModelsTests
    {
        [Fact]
        public void Account_ApplyTransaction_UpdatesBalanceAndTimestamp()
        {
            var account = new Account
            {
                Name = "Test account",
                Currency = "UAH"
            };

            var beforeUpdate = account.UpdatedAt;
            account.ApplyTransaction(250m, new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));

            Assert.Equal(250m, account.Balance);
            Assert.NotEqual(beforeUpdate, account.UpdatedAt);
            Assert.Equal(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc), account.UpdatedAt);
        }

        [Fact]
        public void Budget_ShouldAlert_WhenThresholdExceeded()
        {
            var budget = new Budget
            {
                Name = "Food",
                Limit = 1000m,
                Frequency = BudgetFrequency.Monthly
            };

            budget.RegisterExpense(850m);

            Assert.Equal(150m, budget.Remaining);
            Assert.Equal(85m, budget.UsagePercentage);
            Assert.True(budget.ShouldAlert);
        }

        [Fact]
        public void Budget_RegisterExpense_WithNegativeAmount_ShouldThrow()
        {
            var budget = new Budget
            {
                Name = "Transport",
                Limit = 500m
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => budget.RegisterExpense(-10m));
        }

        [Fact]
        public void Budget_ResetPeriod_ShouldZeroOutSpentAndUpdateDates()
        {
            var budget = new Budget
            {
                Name = "Utilities",
                Limit = 300m,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1)
            };

            budget.RegisterExpense(200m);
            budget.ResetPeriod(BudgetFrequency.Weekly, new DateTime(2025, 2, 1));

            Assert.Equal(0m, budget.Spent);
            Assert.Equal(BudgetFrequency.Weekly, budget.Frequency);
            Assert.Equal(new DateTime(2025, 2, 1), budget.StartDate);
        }

        [Fact]
        public void Account_SetBalance_ShouldOverwriteBalanceAndStampUpdate()
        {
            var account = new Account
            {
                Name = "Primary",
                Currency = "UAH"
            };

            account.SetBalance(1234.56m);

            Assert.Equal(1234.56m, account.Balance);
            Assert.NotNull(account.UpdatedAt);
        }

        [Fact]
        public void RecurringTransaction_CalculateNextOccurrence_RespectsFrequencyAndInterval()
        {
            var startDate = new DateTime(2025, 1, 1);
            var recurring = new RecurringTransaction
            {
                Description = "Gym membership",
                Amount = 500m,
                Frequency = RecurrenceFrequency.Weekly,
                Interval = 2,
                StartDate = startDate
            };

            recurring.CalculateNextOccurrence(startDate);

            Assert.Equal(startDate.AddDays(14), recurring.NextOccurrence);
            Assert.True(recurring.IsDue(startDate.AddDays(15)));

            recurring.EndDate = startDate.AddDays(20);
            recurring.MarkAsExecuted(startDate.AddDays(15));

            Assert.Equal(1, recurring.OccurrenceCount);
            Assert.False(recurring.IsActive);
        }
    }
}
