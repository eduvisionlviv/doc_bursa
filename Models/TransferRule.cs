using System.ComponentModel.DataAnnotations;

namespace doc_bursa.Models
{
    /// <summary>
    /// Правило, яке допомагає ідентифікувати міжсистемний переказ між власними рахунками.
    /// </summary>
    public class TransferRule
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Фраза або назва контрагента, яка вказує на ініціацію переказу (наприклад, "to mono").
        /// </summary>
        [MaxLength(256)]
        public string CounterpartyPattern { get; set; } = string.Empty;

        /// <summary>
        /// Номер рахунку/карти, на який очікується зарахування.
        /// </summary>
        [MaxLength(64)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Джерело (Source) транзакції, з яким слід зіставляти вхідні рухи.
        /// </summary>
        [MaxLength(120)]
        public string TargetSource { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool Matches(Transaction transaction)
        {
            if (!IsActive)
            {
                return false;
            }

            var counterpartyMatch = string.IsNullOrWhiteSpace(CounterpartyPattern)
                || (!string.IsNullOrWhiteSpace(transaction.Counterparty)
                    && transaction.Counterparty.Contains(CounterpartyPattern, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(transaction.Description)
                    && transaction.Description.Contains(CounterpartyPattern, StringComparison.OrdinalIgnoreCase));

            var accountMatch = string.IsNullOrWhiteSpace(AccountNumber)
                || (!string.IsNullOrWhiteSpace(transaction.Account)
                    && transaction.Account.Contains(AccountNumber, StringComparison.OrdinalIgnoreCase));

            return counterpartyMatch && accountMatch;
        }
    }
}
