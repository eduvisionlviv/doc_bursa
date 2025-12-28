using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для звірки та зв'язування переказів між власними рахунками.
    /// Реалізує модуль "Транзит та Звірка" з документації.
    /// </summary>
    public class ReconciliationService
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;

        public ReconciliationService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = Log.ForContext<ReconciliationService>();
        }

        /// <summary>
        /// Отримати всі активні правила звірки.
        /// </summary>
        public async Task<List<ReconciliationRule>> GetActiveRulesAsync(CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            var query = "SELECT * FROM ReconciliationRules WHERE IsActive = 1";
            var table = _databaseService.ExecuteQuery(query);
            
            var rules = new List<ReconciliationRule>();
            foreach (System.Data.DataRow row in table.Rows)
            {
                rules.Add(new ReconciliationRule
                {
                    Id = Convert.ToInt32(row["Id"]),
                    SourceAccountId = Convert.ToInt32(row["SourceAccountId"]),
                    TargetAccountId = Convert.ToInt32(row["TargetAccountId"]),
                    ToleranceAmount = Convert.ToDecimal(row["ToleranceAmount"]),
                    ToleranceHours = Convert.ToInt32(row["ToleranceHours"]),
                    IsActive = Convert.ToBoolean(row["IsActive"])
                });
            }
            
            return rules;
        }

        /// <summary>
        /// Виконати звірку переказів згідно з активними правилами.
        /// </summary>
        public async Task<int> ReconcileTransfersAsync(CancellationToken ct = default)
        {
            var rules = await GetActiveRulesAsync(ct);
            int reconciledCount = 0;

            foreach (var rule in rules)
            {
                reconciledCount += await ReconcileByRuleAsync(rule, ct);
            }

            return reconciledCount;
        }

        private async Task<int> ReconcileByRuleAsync(ReconciliationRule rule, CancellationToken ct)
        {
            // Отримати непарні транзакції з вихідного рахунку
            var outgoingQuery = $@"
                SELECT * FROM Transactions 
                WHERE AccountId = {rule.SourceAccountId} 
                AND Amount < 0 
                AND ReconciliationId IS NULL
                ORDER BY Date";
            
            var outgoingTable = _databaseService.ExecuteQuery(outgoingQuery);
            
            // Отримати непарні транзакції з цільового рахунку
            var incomingQuery = $@"
                SELECT * FROM Transactions 
                WHERE AccountId = {rule.TargetAccountId} 
                AND Amount > 0 
                AND ReconciliationId IS NULL
                ORDER BY Date";
            
            var incomingTable = _databaseService.ExecuteQuery(incomingQuery);
            
            int reconciledCount = 0;

            foreach (System.Data.DataRow outRow in outgoingTable.Rows)
            {
                var outAmount = Math.Abs(Convert.ToDecimal(outRow["Amount"]));
                var outDate = Convert.ToDateTime(outRow["Date"]);
                var outId = Convert.ToInt32(outRow["Id"]);

                foreach (System.Data.DataRow inRow in incomingTable.Rows)
                {
                    var inAmount = Convert.ToDecimal(inRow["Amount"]);
                    var inDate = Convert.ToDateTime(inRow["Date"]);
                    var inId = Convert.ToInt32(inRow["Id"]);

                    // Перевірка умов звірки
                    var amountDiff = Math.Abs(outAmount - inAmount);
                    var timeDiff = Math.Abs((inDate - outDate).TotalHours);

                    if (amountDiff <= rule.ToleranceAmount && timeDiff <= rule.ToleranceHours)
                    {
                        // Створити запис звірки
                        var reconciliationId = await CreateReconciliationAsync(outId, inId, rule.Id);
                        
                        if (reconciliationId > 0)
                        {
                            reconciledCount++;
                            break; // Перейти до наступної вихідної транзакції
                        }
                    }
                }
            }

            return reconciledCount;
        }

        private async Task<int> CreateReconciliationAsync(int outgoingTransactionId, int incomingTransactionId, int ruleId)
        {
            try
            {
                // Вставити запис у таблицю Reconciliations
                var insertQuery = $@"
                    INSERT INTO Reconciliations (OutgoingTransactionId, IncomingTransactionId, RuleId, ReconciliationDate, Status)
                    VALUES ({outgoingTransactionId}, {incomingTransactionId}, {ruleId}, '{DateTime.Now:yyyy-MM-dd HH:mm:ss}', 'Matched');
                    SELECT last_insert_rowid();";
                
                var result = _databaseService.ExecuteQuery(insertQuery);
                var reconciliationId = Convert.ToInt32(result.Rows[0][0]);

                // Оновити транзакції
                var updateOut = $"UPDATE Transactions SET ReconciliationId = {reconciliationId} WHERE Id = {outgoingTransactionId}";
                var updateIn = $"UPDATE Transactions SET ReconciliationId = {reconciliationId} WHERE Id = {incomingTransactionId}";
                
                _databaseService.ExecuteNonQuery(updateOut);
                _databaseService.ExecuteNonQuery(updateIn);

                _logger.Information($"Звірено транзакції {outgoingTransactionId} та {incomingTransactionId}");
                return reconciliationId;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Помилка при створенні звірки для транзакцій {outgoingTransactionId} та {incomingTransactionId}");
                return 0;
            }
        }

        /// <summary>
        /// Скасувати звірку (розв'язати транзакції).
        /// </summary>
        public async Task<bool> UnreconcileAsync(int reconciliationId, CancellationToken ct = default)
        {
            try
            {
                // Видалити ReconciliationId з транзакцій
                var updateQuery = $"UPDATE Transactions SET ReconciliationId = NULL WHERE ReconciliationId = {reconciliationId}";
                _databaseService.ExecuteNonQuery(updateQuery);

                // Видалити запис звірки
                var deleteQuery = $"DELETE FROM Reconciliations WHERE Id = {reconciliationId}";
                _databaseService.ExecuteNonQuery(deleteQuery);

                _logger.Information($"Скасовано звірку {reconciliationId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Помилка при скасуванні звірки {reconciliationId}");
                return false;
            }
        }
    }
}
