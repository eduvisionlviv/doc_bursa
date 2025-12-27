using System;
using System.Collections.Generic;
using FinDesk.Models;

namespace FinDesk.Services
{
    /// <summary>
    /// Сервіс роботи з транзакціями з інтегрованою дедуплікацією.
    /// </summary>
    public class TransactionService
    {
        private readonly DatabaseService _databaseService;
        private readonly DeduplicationService _deduplicationService;

        public TransactionService(DatabaseService databaseService, DeduplicationService deduplicationService)
        {
            _databaseService = databaseService;
            _deduplicationService = deduplicationService;
        }

        public bool AddTransaction(Transaction transaction)
        {
            // prevent unique constraint failures
            if (_databaseService.GetTransactionByTransactionId(transaction.TransactionId) != null)
            {
                return false;
            }

            _deduplicationService.DetectDuplicate(transaction);
            _databaseService.SaveTransaction(transaction);
            return true;
        }

        public List<Transaction> GetTransactions()
        {
            return _databaseService.GetTransactions();
        }

        public bool MarkAsDuplicate(Guid transactionId)
        {
            return _deduplicationService.MarkAsDuplicate(transactionId);
        }

        public int BulkDeduplicate()
        {
            return _deduplicationService.BulkDetectAndMark();
        }
    }
}
