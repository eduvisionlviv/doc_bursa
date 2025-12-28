using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для звірки внутрішніх переказів між рахунками.
    /// </summary>
    public class ReconciliationService
    {
        private const int DateWindowDays = 2;
        private readonly DatabaseService _databaseService;

        public ReconciliationService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Пошук пар списання/зарахування за правилами вікна ±2 дні.
        /// </summary>
        public List<TransferMatch> ReconcileTransfers()
        {
            var rules = _databaseService.GetTransferRules(onlyActive: true);
            var allTransactions = _databaseService.GetTransactions();
            var existingMatches = _databaseService.GetTransferMatches();
            var results = new List<TransferMatch>();

            foreach (var outgoing in allTransactions.Where(t => t.Amount < 0))
            {
                var rule = rules.FirstOrDefault(r => r.Matches(outgoing));
                if (rule == null)
                {
                    continue;
                }

                var windowStart = outgoing.Date.AddDays(-DateWindowDays);
                var windowEnd = outgoing.Date.AddDays(DateWindowDays);

                var incomingCandidates = allTransactions
                    .Where(t => t.Amount > 0
                                && t.Source.Equals(rule.TargetSource, StringComparison.OrdinalIgnoreCase)
                                && t.Date >= windowStart
                                && t.Date <= windowEnd)
                    .OrderBy(t => Math.Abs((t.Date - outgoing.Date).TotalDays))
                    .ToList();

                var existing = existingMatches.FirstOrDefault(m => m.OutgoingTransactionId == outgoing.TransactionId);

                if (incomingCandidates.Any())
                {
                    var best = incomingCandidates
                        .OrderBy(t => Math.Abs(Math.Abs(outgoing.Amount) - t.Amount))
                        .First();

                    var commissionDelta = Math.Abs(outgoing.Amount) - best.Amount;
                    var status = Math.Abs(commissionDelta) < 0.01m
                        ? TransferMatchStatuses.Matched
                        : TransferMatchStatuses.CommissionDelta;

                    var match = existing ?? new TransferMatch
                    {
                        OutgoingTransactionId = outgoing.TransactionId,
                        CreatedAt = DateTime.UtcNow
                    };

                    match.IncomingTransactionId = best.TransactionId;
                    match.CommissionDelta = commissionDelta;
                    match.Status = status;
                    match.UpdatedAt = DateTime.UtcNow;

                    _databaseService.SaveTransferMatch(match);
                    _databaseService.UpdateTransactionTransferInfo(outgoing.Id, true, status, commissionDelta);
                    _databaseService.UpdateTransactionTransferInfo(best.Id, true, status, commissionDelta);

                    results.Add(match);
                }
                else
                {
                    var match = existing ?? new TransferMatch
                    {
                        OutgoingTransactionId = outgoing.TransactionId,
                        Status = TransferMatchStatuses.InTransit,
                        CreatedAt = DateTime.UtcNow
                    };

                    match.UpdatedAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(match.Status))
                    {
                        match.Status = TransferMatchStatuses.InTransit;
                    }

                    _databaseService.SaveTransferMatch(match);
                    _databaseService.UpdateTransactionTransferInfo(outgoing.Id, true, TransferMatchStatuses.InTransit, match.CommissionDelta);
                    results.Add(match);
                }
            }

            return results;
        }

        public void UpdateTransferStatus(Transaction transaction, string status, decimal commission)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            transaction.IsTransfer = true;
            transaction.TransferStatus = status;
            transaction.TransferCommission = commission;

            _databaseService.UpdateTransactionTransferInfo(transaction.Id, true, status, commission);
        }
    }
}
