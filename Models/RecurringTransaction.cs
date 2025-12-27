using System;
using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Модель періодичних (рекурентних) транзакцій.
    /// </summary>
    public class RecurringTransaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Назва періодичного платежу.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Сума транзакції.
        /// </summary>
        [Required]
        public decimal Amount { get; set; }

        /// <summary>
        /// Категорія.
        /// </summary>
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Ідентифікатор рахунку, до якого прив'язана транзакція.
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Посилання на рахунок.
        /// </summary>
        public Account? Account { get; set; }

        /// <summary>
        /// Частота повторення.
        /// </summary>
        [Required]
        public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

        /// <summary>
        /// Інтервал повторення (наприклад, кожні 2 тижні).
        /// </summary>
        public int Interval { get; set; } = 1;

        /// <summary>
        /// Дата початку.
        /// </summary>
        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// Дата закінчення (опціонально).
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Наступна дата виконання.
        /// </summary>
        public DateTime NextOccurrence { get; private set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// Остання дата виконання.
        /// </summary>
        public DateTime? LastOccurrence { get; private set; }

        /// <summary>
        /// Кількість виконань.
        /// </summary>
        public int OccurrenceCount { get; private set; }

        /// <summary>
        /// Чи активна транзакція.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Автоматичне виконання.
        /// </summary>
        public bool AutoExecute { get; set; }

        /// <summary>
        /// Нагадування перед виконанням (днів).
        /// </summary>
        public int ReminderDays { get; set; } = 1;

        /// <summary>
        /// Нотатки.
        /// </summary>
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Дата створення.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата останнього оновлення.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        public RecurringTransaction()
        {
            CalculateNextOccurrence(StartDate);
        }

        /// <summary>
        /// Розрахувати наступну дату виконання.
        /// </summary>
        public void CalculateNextOccurrence(DateTime? fromDate = null)
        {
            var baseDate = fromDate ?? LastOccurrence ?? StartDate;

            NextOccurrence = Frequency switch
            {
                RecurrenceFrequency.Daily => baseDate.AddDays(Interval),
                RecurrenceFrequency.Weekly => baseDate.AddDays(7 * Interval),
                RecurrenceFrequency.Monthly => baseDate.AddMonths(Interval),
                RecurrenceFrequency.Quarterly => baseDate.AddMonths(3 * Interval),
                RecurrenceFrequency.Yearly => baseDate.AddYears(Interval),
                _ => baseDate
            };
        }

        /// <summary>
        /// Перевірити чи настав час для виконання.
        /// </summary>
        public bool IsDue(DateTime? onDate = null)
        {
            var checkDate = onDate?.Date ?? DateTime.UtcNow.Date;
            return IsActive && NextOccurrence.Date <= checkDate;
        }

        /// <summary>
        /// Позначити як виконану.
        /// </summary>
        public void MarkAsExecuted(DateTime? executedAt = null)
        {
            var timestamp = executedAt ?? DateTime.UtcNow;
            LastOccurrence = timestamp;
            OccurrenceCount++;
            CalculateNextOccurrence(timestamp.Date);
            UpdatedAt = timestamp;

            if (EndDate.HasValue && NextOccurrence > EndDate.Value)
            {
                IsActive = false;
            }
        }
    }
}

