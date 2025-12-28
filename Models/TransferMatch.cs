using System;
using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Результат зіставлення переказу між рахунками.
    /// </summary>
    public class TransferMatch
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// TransactionId вихідної транзакції (списання).
        /// </summary>
        [MaxLength(128)]
        public string OutgoingTransactionId { get; set; } = string.Empty;

        /// <summary>
        /// TransactionId вхідної транзакції (зарахування).
        /// </summary>
        [MaxLength(128)]
        public string IncomingTransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Виявлена різниця комісії між вихідною та вхідною сумою.
        /// </summary>
        public decimal CommissionDelta { get; set; }

        /// <summary>
        /// Статус звірки.
        /// </summary>
        [MaxLength(32)]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Дата створення запису.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата останнього оновлення статусу.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsMatched => string.Equals(Status, TransferMatchStatuses.Matched, StringComparison.OrdinalIgnoreCase);
    }

    public static class TransferMatchStatuses
    {
        public const string InTransit = "in_transit";
        public const string Matched = "matched";
        public const string CommissionDelta = "commission_delta";
    }
}
