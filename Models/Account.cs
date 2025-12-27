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
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Назва рахунку (наприклад, "Monobank основний").
        /// </summary>
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Номер рахунку або IBAN.
        /// </summary>
        [MaxLength(34)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Банк чи інституція, що обслуговує рахунок.
        /// </summary>
        [MaxLength(64)]
        public string Institution { get; set; } = string.Empty;

        /// <summary>
        /// Валюта рахунку (ISO код).
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "UAH";

        /// <summary>
        /// Поточний баланс рахунку.
        /// </summary>
        public decimal Balance { get; private set; }

        /// <summary>
        /// Активний/деактивований рахунок.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Дата створення запису.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата останнього оновлення.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Рекурентні транзакції, прив'язані до рахунку.
        /// </summary>
        public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = new List<RecurringTransaction>();

        /// <summary>
        /// Застосувати транзакцію до балансу.
        /// </summary>
        public void ApplyTransaction(decimal amount, DateTime? occurredAt = null)
        {
            Balance += amount;
            UpdatedAt = occurredAt ?? DateTime.UtcNow;
        }

        /// <summary>
        /// Встановити новий баланс (наприклад, після синхронізації з банком).
        /// </summary>
        public void SetBalance(decimal newBalance)
        {
            Balance = newBalance;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
