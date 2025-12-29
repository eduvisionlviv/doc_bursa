using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doc_bursa.Models
{
    public enum TransactionStatus
    {
        Normal,
        InTransit,
        Completed,
        Pending
    }

    /// <summary>
    /// Фінансова транзакція у системі.
    /// </summary>
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(128)]
        public string TransactionId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? TransferId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Range(-999999999.99, 999999999.99)]
        public decimal Amount { get; set; }

        [MaxLength(256)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CategoryName { get; set; } = "Інше";

        [MaxLength(120)]
        public string Source { get; set; } = string.Empty;

        [Required]
        public int AccountId { get; set; }

        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "UAH";

        public bool IsTransfer { get; set; }

        [MaxLength(256)]
        public string Counterparty { get; set; } = string.Empty;

        [NotMapped]
        public decimal Balance => Account?.Balance ?? 0;

        [NotMapped]
        public string Type => Amount >= 0 ? "Дохід" : "Витрата";

        [MaxLength(128)]
        public string Hash { get; set; } = string.Empty;

        public bool IsDuplicate { get; set; }

        [MaxLength(128)]
        public string OriginalTransactionId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string ParentTransactionId { get; set; } = string.Empty;

        public bool IsSplit { get; set; }

        [NotMapped]
        public ObservableCollection<Transaction> Children { get; set; } = new();

        public TransactionStatus Status { get; set; } = TransactionStatus.Normal;

        public decimal? TransferCommission { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
