using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FinDesk.Infrastructure.ExternalApis.Monobank
{
    public class MonobankUserInfoDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("webHookUrl")]
        public string? WebHookUrl { get; set; }

        [JsonPropertyName("permissions")]
        public string Permissions { get; set; } = string.Empty;

        [JsonPropertyName("accounts")]
        public List<MonobankAccountDto> Accounts { get; set; } = new();
    }

    public class MonobankAccountDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("balance")]
        public long Balance { get; set; }

        [JsonPropertyName("creditLimit")]
        public long CreditLimit { get; set; }

        [JsonPropertyName("currencyCode")]
        public int CurrencyCode { get; set; }

        [JsonPropertyName("cashbackType")]
        public string CashbackType { get; set; } = string.Empty;

        [JsonPropertyName("maskedPan")]
        public List<string> MaskedPan { get; set; } = new();

        [JsonPropertyName("iban")]
        public string? Iban { get; set; }
    }

    public class MonobankTransactionDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("mcc")]
        public int Mcc { get; set; }

        [JsonPropertyName("originalMcc")]
        public int OriginalMcc { get; set; }

        [JsonPropertyName("hold")]
        public bool Hold { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("operationAmount")]
        public long OperationAmount { get; set; }

        [JsonPropertyName("currencyCode")]
        public int CurrencyCode { get; set; }

        [JsonPropertyName("commissionRate")]
        public long CommissionRate { get; set; }

        [JsonPropertyName("cashbackAmount")]
        public long CashbackAmount { get; set; }

        [JsonPropertyName("balance")]
        public long Balance { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("receiptId")]
        public string? ReceiptId { get; set; }

        [JsonPropertyName("invoiceId")]
        public string? InvoiceId { get; set; }

        [JsonPropertyName("counterEdrpou")]
        public string? CounterEdrpou { get; set; }

        [JsonPropertyName("counterIban")]
        public string? CounterIban { get; set; }

        [JsonPropertyName("counterName")]
        public string? CounterName { get; set; }
    }

    public class MonobankCurrencyRateDto
    {
        [JsonPropertyName("currencyCodeA")]
        public int CurrencyCodeA { get; set; }

        [JsonPropertyName("currencyCodeB")]
        public int CurrencyCodeB { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("rateBuy")]
        public decimal? RateBuy { get; set; }

        [JsonPropertyName("rateSell")]
        public decimal? RateSell { get; set; }

        [JsonPropertyName("rateCross")]
        public decimal? RateCross { get; set; }
    }
}

