using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using doc_bursa.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для модуля Бюджетування та Планування.
    /// Реалізує календар платежів, Plan/Fact механізм, розрахунок Вільних коштів.
    /// </summary>
    public class BudgetService
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;

        public BudgetService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = Log.ForContext<BudgetService>();
        }

        /// <summary>
        /// Створити планову транзакцію.
        /// </summary>
        public PlannedTransaction CreatePlannedTransaction(PlannedTransaction plannedTransaction)
        {
            try
            {
                _databaseService.ExecuteNonQuery(
                    @"INSERT INTO PlannedTransactions (AccountNumber, PlannedDate, Amount, Description, Category, IsExecuted, ActualTransactionId)
                      VALUES (@accountNumber, @plannedDate, @amount, @description, @category, @isExecuted, @actualTransactionId)",
                    new SqliteParameter("@accountNumber", plannedTransaction.AccountNumber),
                    new SqliteParameter("@plannedDate", plannedTransaction.PlannedDate),
                    new SqliteParameter("@amount", plannedTransaction.Amount),
                    new SqliteParameter("@description", plannedTransaction.Description ?? (object)DBNull.Value),
                    new SqliteParameter("@category", plannedTransaction.Category ?? (object)DBNull.Value),
                    new SqliteParameter("@isExecuted", plannedTransaction.IsExecuted ? 1 : 0),
                    new SqliteParameter("@actualTransactionId", plannedTransaction.ActualTransactionId ?? (object)DBNull.Value)
                );

                plannedTransaction.Id = (int)_databaseService.ExecuteScalar("SELECT last_insert_rowid()");
                _logger.Information("Created planned transaction {Id}", plannedTransaction.Id);
                return plannedTransaction;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating planned transaction");
                throw;
            }
        }

        /// <summary>
        /// Отримати всі планові транзакції для рахунку.
        /// </summary>
        public List<PlannedTransaction> GetPlannedTransactions(string accountNumber)
        {
            var transactions = new List<PlannedTransaction>();
            var table = _databaseService.ExecuteQuery(
                "SELECT * FROM PlannedTransactions WHERE AccountNumber = @accountNumber AND IsExecuted = 0 ORDER BY PlannedDate",
                new SqliteParameter("@accountNumber", accountNumber)
            );

            foreach (DataRow row in table.Rows)
            {
                transactions.Add(MapToPlannedTransaction(row));
            }

            return transactions;
        }

        /// <summary>
        /// Позначити планову транзакцію як виконану.
        /// </summary>
        public void MarkAsExecuted(int plannedTransactionId, int actualTransactionId)
        {
            try
            {
                _databaseService.ExecuteNonQuery(
                    "UPDATE PlannedTransactions SET IsExecuted = 1, ActualTransactionId = @actualTransactionId WHERE Id = @id",
                    new SqliteParameter("@actualTransactionId", actualTransactionId),
                    new SqliteParameter("@id", plannedTransactionId)
                );
                _logger.Information("Marked planned transaction {Id} as executed", plannedTransactionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking planned transaction as executed");
                throw;
            }
        }

        /// <summary>
        /// Розрахувати \"Вільні кошти\" (Free Cash) для рахунку.
        /// Free Cash = Поточний Баланс - Сума Планових Витрат (до кінця періоду).
        /// </summary>
        public decimal CalculateFreeCash(string accountNumber, DateTime endDate)
        {
            try
            {
                var balance = _databaseService.ExecuteScalar(
                    "SELECT Balance FROM Accounts WHERE AccountNumber = @accountNumber",
                    new SqliteParameter("@accountNumber", accountNumber)
                );

                if (balance == null || balance == DBNull.Value)
                    return 0m;

                var currentBalance = Convert.ToDecimal(balance);

                var plannedExpenses = _databaseService.ExecuteScalar(
                    @"SELECT COALESCE(SUM(Amount), 0) FROM PlannedTransactions 
                      WHERE AccountNumber = @accountNumber AND IsExecuted = 0 
                      AND PlannedDate <= @endDate AND Amount < 0",
                    new SqliteParameter("@accountNumber", accountNumber),
                    new SqliteParameter("@endDate", endDate)
                );

                var expenses = plannedExpenses != null && plannedExpenses != DBNull.Value
                    ? Convert.ToDecimal(plannedExpenses)
                    : 0m;

                return currentBalance + expenses;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating free cash");
                throw;
            }
        }

        /// <summary>
        /// Отримати всі планові транзакції в діапазоні дат.
        /// </summary>
        public List<PlannedTransaction> GetPlannedTransactionsByDateRange(
            string accountNumber, DateTime startDate, DateTime endDate)
        {
            var transactions = new List<PlannedTransaction>();
            var table = _databaseService.ExecuteQuery(
                @"SELECT * FROM PlannedTransactions 
                  WHERE AccountNumber = @accountNumber 
                  AND PlannedDate >= @startDate 
                  AND PlannedDate <= @endDate 
                  ORDER BY PlannedDate",
                new SqliteParameter("@accountNumber", accountNumber),
                new SqliteParameter("@startDate", startDate),
                new SqliteParameter("@endDate", endDate)
            );

            foreach (DataRow row in table.Rows)
            {
                transactions.Add(MapToPlannedTransaction(row));
            }

            return transactions;
        }

        /// <summary>
        /// Видалити планову транзакцію.
        /// </summary>
        public void DeletePlannedTransaction(int plannedTransactionId)
        {
            try
            {
                _databaseService.ExecuteNonQuery(
                    "DELETE FROM PlannedTransactions WHERE Id = @id",
                    new SqliteParameter("@id", plannedTransactionId)
                );              _logger.Information("Deleted planned transaction {Id}", plannedTransactionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting planned transaction");
                throw;
            }
        }

        private PlannedTransaction MapToPlannedTransaction(DataRow row)
        {
            return new PlannedTransaction
            {
                Id = Convert.ToInt32(row["Id"]),
                AccountNumber = row["AccountNumber"].ToString(),
                PlannedDate = Convert.ToDateTime(row["PlannedDate"]),
                Amount = Convert.ToDecimal(row["Amount"]),
                Description = row["Description"] != DBNull.Value ? row["Description"].ToString() : null,
                Category = row["Category"] != DBNull.Value ? row["Category"].ToString() : null,
                IsExecuted = Convert.ToBoolean(row["IsExecuted"]),
                ActualTransactionId = row["ActualTransactionId"] != DBNull.Value ? Convert.ToInt32(row["ActualTransactionId"]) : null
            };
        }
    }
}
