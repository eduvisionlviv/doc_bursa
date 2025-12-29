using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Банківський або фінансовий рахунок користувача.
    /// </summary>
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(34)]
        public string AccountNumber { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Institution { get; set; } = string.Empty;

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "UAH";

        public int? AccountGroupId { get; set; }

        public AccountGroup? AccountGroup { get; set; }

        public decimal Balance { get; internal set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = new List<RecurringTransaction>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        public void ApplyTransaction(decimal amount, DateTime? occurredAt = null)
        {
            Balance += amount;
            UpdatedAt = occurredAt ?? DateTime.UtcNow;
        }

        public void SetBalance(decimal newBalance)
        {
            Balance = newBalance;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
