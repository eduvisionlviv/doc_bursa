using System;
using System.ComponentModel.DataAnnotations;

namespace FinDesk.Models
{
    // Модель бюджету для контролю витрат та прибутків
    public class Budget
    {
        [Key]
        public int Id { get; set; }

        // Назва бюджету
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        // Категорія для якої встановлено бюджет
        [MaxLength(100)]
        public string Category { get; set; }

        // Ліміт бюджету
        [Required]
        public decimal Limit { get; set; }

        // Поточна витрачена сума
        public decimal Spent { get; set; }

        // Період бюджету (щомісячний, щотижневий, щорічний)
        [Required]
        [MaxLength(20)]
        public string Period { get; set; } // "Monthly", "Weekly", "Yearly"

        // Дата початку періоду
        public DateTime StartDate { get; set; }

        // Дата закінчення періоду
        public DateTime EndDate { get; set; }

        // Чи активний бюджет
        public bool IsActive { get; set; } = true;

        // Сповіщення при досягненні відсотка використання
        public int AlertThreshold { get; set; } = 80; // За замовчуванням 80%

        // Опис бюджету
        [MaxLength(500)]
        public string Description { get; set; }

        // Дата створення
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Дата останнього оновлення
        public DateTime? UpdatedAt { get; set; }

        // Розраховані властивості
        
        // Залишок бюджету
        public decimal Remaining => Limit - Spent;

        // Відсоток використання бюджету
        public decimal UsagePercentage => Limit > 0 ? (Spent / Limit) * 100 : 0;

        // Чи перевищено бюджет
        public bool IsOverBudget => Spent > Limit;

        // Чи досягнуто порогу сповіщення
        public bool ShouldAlert => UsagePercentage >= AlertThreshold;
    }
}
