using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doc_bursa.Models
{
    /// <summary>
    /// Група рахунків для об'єднання та аналізу
    /// </summary>
    public class AccountGroup
    {
        [Key]
        public int Id { get; set; }
        
        /// <summary>
        /// Назва групи (напр.: "Особисті рахунки", "Бізнес")
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Опис групи
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Колір для візуалізації (HEX: #FF5733)
        /// </summary>
        public string Color { get; set; } = "#2196F3";
        
        /// <summary>
        /// Іконка групи (Material Design Icons)
        /// </summary>
        public string Icon { get; set; } = "AccountMultiple";
        
        /// <summary>
        /// Список ID джерел даних в цій групі
        /// </summary>
        [NotMapped]
        public List<int> SourceIds { get; set; } = new();
        
        /// <summary>
        /// Дата створення
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Чи група активна
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Порядок відображення
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Посилання на майстер-групи з композитним ключем.
        /// </summary>
        public ICollection<MasterGroupAccountGroup> MasterGroupLinks { get; set; } = new List<MasterGroupAccountGroup>();
        
        /// <summary>
        /// Розраховує загальний баланс групи
        /// </summary>
        public decimal GetTotalBalance(List<Transaction> transactions)
        {
            return transactions
                .Where(t => SourceIds.Any(id => t.Source?.Contains(id.ToString()) == true))
                .Sum(t => t.Amount);
        }
        
        /// <summary>
        /// Розраховує приходи за період
        /// </summary>
        public decimal GetIncome(List<Transaction> transactions, DateTime from, DateTime to)
        {
            return transactions
                .Where(t => t.Amount > 0 && 
                           t.Date >= from && 
                           t.Date <= to &&
                           SourceIds.Any(id => t.Source?.Contains(id.ToString()) == true))
                .Sum(t => t.Amount);
        }
        
        /// <summary>
        /// Розраховує витрати за період
        /// </summary>
        public decimal GetExpenses(List<Transaction> transactions, DateTime from, DateTime to)
        {
            return Math.Abs(transactions
                .Where(t => t.Amount < 0 && 
                           t.Date >= from && 
                           t.Date <= to &&
                           SourceIds.Any(id => t.Source?.Contains(id.ToString()) == true))
                .Sum(t => t.Amount));
        }
    }
}

