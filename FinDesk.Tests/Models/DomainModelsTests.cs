using System;
using FinDesk.Models;
using NUnit.Framework;

namespace FinDesk.Tests.Models
{
    [TestFixture]
    public class DomainModelsTests
    {
        [Test]
        public void Account_ApplyTransaction_UpdatesBalanceAndTimestamp()
        {
            var account = new Account
            {
                Name = "Test account",
                Currency = "UAH"
            };

            var beforeUpdate = account.UpdatedAt;
            account.ApplyTransaction(250m, new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));

            Assert.That(account.Balance, Is.EqualTo(250m));
            Assert.That(account.UpdatedAt, Is.Not.EqualTo(beforeUpdate));
            Assert.That(account.UpdatedAt, Is.EqualTo(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void Budget_ShouldAlert_WhenThresholdExceeded()
        {
            var budget = new Budget
            {
                Name = "Food",
                Limit = 1000m,
                Frequency = BudgetFrequency.Monthly
            };

            budget.RegisterExpense(850m);

            Assert.That(budget.Remaining, Is.EqualTo(150m));
            Assert.That(budget.UsagePercentage, Is.EqualTo(85m));
            Assert.That(budget.ShouldAlert, Is.True);
        }

        [Test]
        public void Budget_RegisterExpense_WithNegativeAmount_ShouldThrow()
        {
            var budget = new Budget
            {
                Name = "Transport",
                Limit = 500m
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => budget.RegisterExpense(-10m));
        }

        [Test]
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

            Assert.That(budget.Spent, Is.EqualTo(0m));
            Assert.That(budget.Frequency, Is.EqualTo(BudgetFrequency.Weekly));
            Assert.That(budget.StartDate, Is.EqualTo(new DateTime(2025, 2, 1)));
        }

        [Test]
        public void Account_SetBalance_ShouldOverwriteBalanceAndStampUpdate()
        {
            var account = new Account
            {
                Name = "Primary",
                Currency = "UAH"
            };

            account.SetBalance(1234.56m);

            Assert.That(account.Balance, Is.EqualTo(1234.56m));
            Assert.That(account.UpdatedAt, Is.Not.Null);
        }

        [Test]
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

            Assert.That(recurring.NextOccurrence, Is.EqualTo(startDate.AddDays(14)));
            Assert.That(recurring.IsDue(startDate.AddDays(15)), Is.True);

            recurring.EndDate = startDate.AddDays(20);
            recurring.MarkAsExecuted(startDate.AddDays(15));

            Assert.That(recurring.OccurrenceCount, Is.EqualTo(1));
            Assert.That(recurring.IsActive, Is.False);
        }
    }
}
