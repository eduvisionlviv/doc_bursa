using System;
using System.ComponentModel.DataAnnotations;

namespace FinDesk.Models
{
    /// <summary>
    /// Модель бюджету для контролю витрат та прибутків.
    /// </summary>
    public class Budget
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Назва бюджету.
        /// </summary>
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Категорія для якої встановлено бюджет.
        /// </summary>
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Ліміт бюджету.
        /// </summary>
        [Required]
        public decimal Limit { get; set; }

        /// <summary>
        /// Поточна витрачена сума.
        /// </summary>
        public decimal Spent { get; set; }

        /// <summary>
        /// Період бюджету.
        /// </summary>
        [Required]
        public BudgetFrequency Frequency { get; set; } = BudgetFrequency.Monthly;

        /// <summary>
        /// Дата початку періоду.
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// Дата закінчення періоду (якщо застосовується).
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Чи активний бюджет.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Поріг сповіщення у відсотках.
        /// </summary>
        public int AlertThreshold { get; set; } = 80;

        /// <summary>
        /// Опис бюджету.
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Дата створення запису.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата останнього оновлення.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Залишок бюджету.
        /// </summary>
        public decimal Remaining => Limit - Spent;

        /// <summary>
        /// Місячний ліміт бюджету (alias для Limit).
        /// </summary>
        public decimal MonthlyLimit
        {
            get => Limit;
            set => Limit = value;
        }

        /// <summary>
        /// Сумісність з періодом у вигляді рядка.
        /// </summary>
        public string Period
        {
            get => Frequency.ToString();
            set
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    Enum.TryParse<BudgetFrequency>(value, true, out var parsed))
                {
                    Frequency = parsed;
                }
            }
        }

        /// <summary>
        /// Відсоток використання бюджету.
        /// </summary>
        public decimal UsagePercentage => Limit > 0 ? Math.Round((Spent / Limit) * 100, 2) : 0;

        /// <summary>
        /// Чи перевищено бюджет.
        /// </summary>
        public bool IsOverBudget => Spent > Limit;

        /// <summary>
        /// Чи досягнуто порогу сповіщення.
        /// </summary>
        public bool ShouldAlert => UsagePercentage >= AlertThreshold;

        /// <summary>
        /// Зареєструвати нову витрату.
        /// </summary>
        public void RegisterExpense(decimal amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
            }

            Spent += amount;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Скинути показники для нового періоду.
        /// </summary>
        public void ResetPeriod(BudgetFrequency? newFrequency = null, DateTime? startDate = null)
        {
            Spent = 0;
            Frequency = newFrequency ?? Frequency;
            StartDate = startDate?.Date ?? DateTime.UtcNow.Date;
            EndDate = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
