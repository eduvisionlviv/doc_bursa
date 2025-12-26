using System;

namespace FinDesk.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Інше";
        public string Source { get; set; } = string.Empty;
        public string Type => Amount >= 0 ? "Дохід" : "Витрата";
        public string Hash { get; set; } = string.Empty;
    }
}



