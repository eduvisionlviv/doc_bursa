using System;
using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Статус планової транзакції.
    /// </summary>
    public enum PlannedTransactionStatus
    {
        /// <summary>Очікує виконання</summary>
        Pending,
        /// <summary>Виконано (поглинуто реальною транзакцією)</summary>
        Completed,
        /// <summary>Пропущено (не відбулося)</summary>
        Skipped
    }

    /// <summary>
    /// Планова транзакція для модуля Бюджетування.
    /// Використовується для розрахунку Вільних коштів (Free Cash).
    /// </summary>
    public class PlannedTransaction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Назва планового платежу.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Опис.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Планова дата виконання.
        /// </summary>
        public DateTime PlannedDate { get; set; }

        /// <summary>
        /// Планова сума (негативна для витрат, позитивна для доходу).
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Категорія.
        /// </summary>
        [MaxLength(100)]
        public string Category { get; set; } = "Інше";

        /// <summary>
        /// Рахунок, на якому очікується транзакція.
        /// </summary>
        public int AccountId { get; set; }

        /// <summary>
        /// Статус плану.
        /// </summary>
        public PlannedTransactionStatus Status { get; set; } = PlannedTransactionStatus.Pending;

        /// <summary>
        /// ID реальної транзакції, яка "поглинула" планову.
        /// </summary>
        public int? ActualTransactionId { get; set; }

        /// <summary>
        /// ID шаблону регулярного платежу (якщо створено автоматично).
        /// </summary>
        public int? RecurringTransactionId { get; set; }

        /// <summary>
        /// Чи це регулярний платіж.
        /// </summary>
        public bool IsRecurring { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
