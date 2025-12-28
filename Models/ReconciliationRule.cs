using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Правило для автоматичного зв'язування переказів між власними рахунками.
    /// Rule-Based система для модуля "Транзит та Звірка".
    /// </summary>
    public class ReconciliationRule
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Назва правила (для відображення користувачу).
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Рахунок-джерело (звідки йде переказ).
        /// </summary>
        public int SourceAccountId { get; set; }

        /// <summary>
        /// Рахунок-призначення (куди приходить переказ).
        /// </summary>
        public int TargetAccountId { get; set; }

        /// <summary>
        /// Умова: контрагент містить (напр. "Іванов І.І.").
        /// </summary>
        [MaxLength(256)]
        public string? CounterpartyPattern { get; set; }

        /// <summary>
        /// Умова: номер рахунку контрагента містить.
        /// </summary>
        [MaxLength(100)]
        public string? AccountNumberPattern { get; set; }

        /// <summary>
        /// Максимальна різниця в днях між парними транзакціями.
        /// </summary>
        public int MaxDaysDifference { get; set; } = 2;

        /// <summary>
        /// Максимально допустима комісія у відсотках (для розбіжності сум).
        /// </summary>
        public decimal MaxCommissionPercent { get; set; } = 2.0m;

        /// <summary>
        /// Чи правило активне.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Категорія для витрати комісії.
        /// </summary>
        [MaxLength(100)]
        public string CommissionCategory { get; set; } = "Банківська комісія";
    }
}
