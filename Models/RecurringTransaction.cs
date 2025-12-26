using System;
using System.ComponentModel.DataAnnotations;

namespace FinDesk.Models
{
    // Модель періодичних (рекурентних) транзакцій
    public class RecurringTransaction
    {
        [Key]
        public int Id { get; set; }

        // Назва періодичного платежу
        [Required]
        [MaxLength(200)]
        public string Description { get; set; }

        // Сума транзакції
        [Required]
        public decimal Amount { get; set; }

        // Категорія
        [MaxLength(100)]
        public string Category { get; set; }

        // Рахунок
        [MaxLength(100)]
        public string Account { get; set; }

        // Частота повторення (Daily, Weekly, Monthly, Yearly)
        [Required]
        [MaxLength(20)]
        public string Frequency { get; set; }

        // Інтервал повторення (наприклад, кожні 2 тижні)
        public int Interval { get; set; } = 1;

        // Дата початку
        [Required]
        public DateTime StartDate { get; set; }

        // Дата закінчення (опціонально)
        public DateTime? EndDate { get; set; }

        // Наступна дата виконання
        public DateTime NextOccurrence { get; set; }

        // Остання дата виконання
        public DateTime? LastOccurrence { get; set; }

        // Кількість виконань
        public int OccurrenceCount { get; set; } = 0;

        // Чи активна транзакція
        public bool IsActive { get; set; } = true;

        // Автоматичне виконання
        public bool AutoExecute { get; set; } = false;

        // Нагадування перед виконанням (днів)
        public int ReminderDays { get; set; } = 1;

        // Нотатки
        [MaxLength(500)]
        public string Notes { get; set; }

        // Дата створення
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Дата останнього оновлення
        public DateTime? UpdatedAt { get; set; }

        // Методи для роботи з періодичністю

        /// <summary>
        /// Розрахувати наступну дату виконання
        /// </summary>
        public void CalculateNextOccurrence()
        {
            var baseDate = LastOccurrence ?? StartDate;

            switch (Frequency.ToLower())
            {
                case "daily":
                    NextOccurrence = baseDate.AddDays(Interval);
                    break;
                case "weekly":
                    NextOccurrence = baseDate.AddDays(7 * Interval);
                    break;
                case "monthly":
                    NextOccurrence = baseDate.AddMonths(Interval);
                    break;
                case "yearly":
                    NextOccurrence = baseDate.AddYears(Interval);
                    break;
                default:
                    NextOccurrence = baseDate.AddMonths(1);
                    break;
            }
        }

        /// <summary>
        /// Перевірити чи настав час для виконання
        /// </summary>
        public bool IsDue()
        {
            return IsActive && NextOccurrence.Date <= DateTime.Today;
        }

        /// <summary>
        /// Позначити як виконану
        /// </summary>
        public void MarkAsExecuted()
        {
            LastOccurrence = DateTime.Now;
            OccurrenceCount++;
            CalculateNextOccurrence();
            UpdatedAt = DateTime.Now;

            // Перевірити чи не закінчилась транзакція
            if (EndDate.HasValue && NextOccurrence > EndDate.Value)
            {
                IsActive = false;
            }
        }
    }
}
