using System;
using System.Linq;
using System.Threading.Tasks;
using FinDesk.Infrastructure.Data;
using FinDesk.Infrastructure.Repositories;
using FinDesk.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinDesk.Tests
{
    public class RepositoryTests
    {
        private static FinDeskDbContext CreateContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<FinDeskDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            return new FinDeskDbContext(options);
        }

        [Fact]
        public async Task TransactionRepository_PerformsCrudOperations()
        {
            using var context = CreateContext(nameof(TransactionRepository_PerformsCrudOperations));
            var repository = new TransactionRepository(context);

            var transaction = new Transaction
            {
                TransactionId = "tx-1",
                Date = DateTime.UtcNow,
                Amount = 150.25m,
                Description = "Groceries",
                Category = "Продукти",
                Source = "Manual",
                Hash = "hash-1"
            };

            await repository.AddAsync(transaction);
            await repository.SaveChangesAsync();

            var saved = await repository.GetByIdAsync(transaction.Id);
            Assert.NotNull(saved);
            Assert.Equal("tx-1", saved!.TransactionId);

            saved.Description = "Updated description";
            repository.Update(saved);
            await repository.SaveChangesAsync();

            var updated = await repository.GetByIdAsync(transaction.Id);
            Assert.Equal("Updated description", updated!.Description);

            repository.Delete(updated);
            await repository.SaveChangesAsync();

            var all = await repository.GetAllAsync();
            Assert.DoesNotContain(all, t => t.Id == transaction.Id);
        }

        [Fact]
        public async Task AccountRepository_PerformsCrudOperations()
        {
            using var context = CreateContext(nameof(AccountRepository_PerformsCrudOperations));
            var repository = new AccountRepository(context);

            var account = new Account
            {
                Name = "Test Account",
                Source = "Bank",
                Balance = 500m
            };

            await repository.AddAsync(account);
            await repository.SaveChangesAsync();

            var saved = await repository.GetByIdAsync(account.Id);
            Assert.NotNull(saved);
            Assert.Equal("Test Account", saved!.Name);

            saved.Balance = 750m;
            repository.Update(saved);
            await repository.SaveChangesAsync();

            var updated = await repository.GetByIdAsync(account.Id);
            Assert.Equal(750m, updated!.Balance);

            repository.Delete(updated);
            await repository.SaveChangesAsync();

            var all = await repository.GetAllAsync();
            Assert.DoesNotContain(all, a => a.Id == account.Id);
        }

        [Fact]
        public async Task CategoryRepository_PerformsCrudOperations()
        {
            using var context = CreateContext(nameof(CategoryRepository_PerformsCrudOperations));
            var repository = new CategoryRepository(context);

            var initialCount = (await repository.GetAllAsync()).Count;

            var category = new Category
            {
                Name = "Нова категорія",
                Amount = 0,
                Count = 0
            };

            await repository.AddAsync(category);
            await repository.SaveChangesAsync();

            var saved = await repository.GetByIdAsync(category.Id);
            Assert.NotNull(saved);
            Assert.Equal("Нова категорія", saved!.Name);

            saved.Amount = 100;
            repository.Update(saved);
            await repository.SaveChangesAsync();

            var updated = await repository.GetByIdAsync(category.Id);
            Assert.Equal(100, updated!.Amount);

            repository.Delete(updated);
            await repository.SaveChangesAsync();

            var finalCount = (await repository.GetAllAsync()).Count;
            Assert.Equal(initialCount, finalCount);
        }

        [Fact]
        public async Task BudgetRepository_PerformsCrudOperations()
        {
            using var context = CreateContext(nameof(BudgetRepository_PerformsCrudOperations));
            var repository = new BudgetRepository(context);

            var budget = new Budget
            {
                Name = "Monthly Groceries",
                Category = "Продукти",
                Limit = 1000m,
                Spent = 200m,
                Frequency = BudgetFrequency.Monthly,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddMonths(1),
                IsActive = true,
                AlertThreshold = 80,
                Description = "Groceries budget"
            };

            await repository.AddAsync(budget);
            await repository.SaveChangesAsync();

            var saved = await repository.GetByIdAsync(budget.Id);
            Assert.NotNull(saved);
            Assert.Equal("Monthly Groceries", saved!.Name);

            saved.Spent = 400m;
            repository.Update(saved);
            await repository.SaveChangesAsync();

            var updated = await repository.GetByIdAsync(budget.Id);
            Assert.Equal(400m, updated!.Spent);

            repository.Delete(updated);
            await repository.SaveChangesAsync();

            var all = await repository.GetAllAsync();
            Assert.DoesNotContain(all, b => b.Id == budget.Id);
        }

        [Fact]
        public async Task RecurringTransactionRepository_PerformsCrudOperations()
        {
            using var context = CreateContext(nameof(RecurringTransactionRepository_PerformsCrudOperations));
            var repository = new RecurringTransactionRepository(context);

            var recurring = new RecurringTransaction
            {
                Description = "Gym membership",
                Amount = 50m,
                Category = "Здоров'я",
                Account = "Основний",
                Frequency = "Monthly",
                Interval = 1,
                StartDate = DateTime.UtcNow.Date,
                NextOccurrence = DateTime.UtcNow.Date.AddMonths(1),
                IsActive = true,
                ReminderDays = 3
            };

            await repository.AddAsync(recurring);
            await repository.SaveChangesAsync();

            var saved = await repository.GetByIdAsync(recurring.Id);
            Assert.NotNull(saved);
            Assert.Equal("Gym membership", saved!.Description);

            saved.Amount = 60m;
            repository.Update(saved);
            await repository.SaveChangesAsync();

            var updated = await repository.GetByIdAsync(recurring.Id);
            Assert.Equal(60m, updated!.Amount);

            repository.Delete(updated);
            await repository.SaveChangesAsync();

            var all = await repository.GetAllAsync();
            Assert.DoesNotContain(all, r => r.Id == recurring.Id);
        }
    }
}

