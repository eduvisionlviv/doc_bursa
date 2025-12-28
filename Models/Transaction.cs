using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Статус транзакції-переказу між власними рахунками.
    /// </summary>
    public enum TransactionStatus
    {
        /// <summary>Звичайна транзакція</summary>
        Normal,
        /// <summary>Переказ в процесі (пара не знайдена)</summary>
        InTransit,
        /// <summary>Переказ завершено (пара знайдена)</summary>
        Completed,
        /// <summary>Очікує обробки</summary>
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

        /// <summary>
        /// Ідентифікатор парної транзакції для переказів між власними рахунками.
        /// Зв'язує дві транзакції: витрату на одному рахунку та надходження на іншому.
        /// </summary>
        [MaxLength(128)]
        public string? TransferId { get; set; }

        /// <summary>
        /// Дата транзакції в базовій моделі.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Альтернативна назва для сумісності з сервісами.
        /// </summary>
        public DateTime TransactionDate
        {
            get => Date;
            set => Date = value;
        }

        public decimal Amount { get; set; }

        [MaxLength(256)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Category { get; set; } = "Інше";

        [MaxLength(120)]
        public string Source { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Account { get; set; } = string.Empty;

        public int AccountId { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "UAH";

        public bool IsTransfer { get; set; }

        [MaxLength(256)]
        public string Counterparty { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public string Type => Amount >= 0 ? "Дохід" : "Витрата";

        [MaxLength(128)]
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Позначка про те, що транзакція є дублікатом іншої.
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Ідентифікатор оригінальної транзакції (TransactionId), якщо поточна є дублікатом.
        /// </summary>
        [MaxLength(128)]
        public string OriginalTransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Посилання на батьківську транзакцію у разі спліту.
        /// </summary>
        [MaxLength(128)]
        public string ParentTransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Позначка, що транзакція була розбита на дочірні.
        /// </summary>
        public bool IsSplit { get; set; }

        /// <summary>
        /// Дерево дочірніх транзакцій для відображення у UI.
        /// </summary>
        public ObservableCollection<Transaction> Children { get; set; } = new();

        /// <summary>
        /// Статус переказу між власними рахунками.
        /// </summary>
        public TransactionStatus Status { get; set; } = TransactionStatus.Normal;

        /// <summary>
        /// Комісія за переказ між власними рахунками.
        /// </summary>
        public decimal? TransferCommission { get; set; }
    }
}
