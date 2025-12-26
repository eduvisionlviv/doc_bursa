using System;

namespace FinDesk.Models;

public sealed class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "import";
    public string Account { get; set; } = "";
    public DateTime DateUtc { get; set; }
    public string Description { get; set; } = "";
    public string Merchant { get; set; } = "";
    public decimal Amount { get; set; }   // + income, - expense
    public string Currency { get; set; } = "UAH";
    public MoneyCategory Category { get; set; } = MoneyCategory.Unsorted;
    public string Hash { get; set; } = "";
    public string RawJson { get; set; } = "";
}
