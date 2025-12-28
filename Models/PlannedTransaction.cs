using System;

namespace doc_bursa.Models
{
    /// <summary>
    /// Представлення запланованої транзакції на основі рекурентного правила.
    /// </summary>
    public class PlannedTransaction
    {
        public Guid RecurringId { get; set; }
        public DateTime PlannedDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public bool IsPlanned { get; set; } = true;
        public string LinkedTransactionId { get; set; } = string.Empty;

        public bool IsAbsorbed => !string.IsNullOrWhiteSpace(LinkedTransactionId);
        public bool IsWarning => !IsAbsorbed && PlannedDate.Date <= DateTime.UtcNow.Date.AddDays(3);
    }
}
