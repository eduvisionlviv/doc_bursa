using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Хелпер для нормалізації сум, валют та текстових полів транзакцій.
    /// </summary>
    public static class NormalizationHelper
    {
        private static readonly Dictionary<string, string> CurrencyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["₴"] = "UAH",
            ["грн"] = "UAH",
            ["uah"] = "UAH",
            ["$"] = "USD",
            ["usd"] = "USD",
            ["€"] = "EUR",
            ["eur"] = "EUR",
            ["₽"] = "RUB",
            ["rub"] = "RUB",
            ["£"] = "GBP",
            ["gbp"] = "GBP",
            ["pln"] = "PLN",
            ["zł"] = "PLN"
        };

        public static bool TryNormalizeAmount(string? rawAmount, string? currencyHint, out decimal amount, out string currencyCode)
        {
            amount = 0m;
            currencyCode = NormalizeCurrency(currencyHint);

            if (string.IsNullOrWhiteSpace(rawAmount))
            {
                return false;
            }

            var detectedCurrency = DetectCurrency(rawAmount);
            if (!string.IsNullOrWhiteSpace(detectedCurrency))
            {
                currencyCode = detectedCurrency;
            }

            var sanitized = rawAmount.Trim();
            var isNegative = sanitized.Contains("(") && sanitized.Contains(")");
            sanitized = sanitized.Replace("(", string.Empty).Replace(")", string.Empty);

            if (sanitized.EndsWith("-", StringComparison.Ordinal))
            {
                isNegative = true;
                sanitized = sanitized.TrimEnd('-');
            }

            sanitized = sanitized.TrimStart('+').Trim();
            sanitized = RemoveCurrencySymbols(sanitized);
            sanitized = sanitized.Replace("\u00A0", string.Empty).Replace(" ", string.Empty);
            sanitized = NormalizeDecimalSeparators(sanitized);

            if (!decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) &&
                !decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
            {
                return false;
            }

            if (isNegative && amount > 0)
            {
                amount = -amount;
            }

            amount = NormalizeAmount(amount);
            return true;
        }

        public static decimal NormalizeAmount(decimal amount)
        {
            return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        public static string NormalizeCurrency(string? currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                return "UAH";
            }

            var trimmed = currency.Trim();
            return CurrencyMap.TryGetValue(trimmed, out var mapped)
                ? mapped
                : trimmed.ToUpperInvariant();
        }

        public static void NormalizeTransaction(Transaction transaction, string? currencyCode = null)
        {
            transaction.Description = transaction.Description?.Trim() ?? string.Empty;
            transaction.Category = transaction.Category?.Trim() ?? string.Empty;
            transaction.Source = transaction.Source?.Trim() ?? string.Empty;
            transaction.Counterparty = transaction.Counterparty?.Trim() ?? string.Empty;
            transaction.Account = transaction.Account?.Trim() ?? string.Empty;
            transaction.Hash = transaction.Hash?.Trim() ?? string.Empty;
            transaction.OriginalTransactionId = transaction.OriginalTransactionId?.Trim() ?? string.Empty;
            transaction.Amount = NormalizeAmount(transaction.Amount);

            if (string.IsNullOrWhiteSpace(transaction.TransactionId))
            {
                transaction.TransactionId = $"{transaction.Source}-{transaction.Date:yyyyMMdd}-{Guid.NewGuid():N}";
            }
        }

        private static string DetectCurrency(string value)
        {
            foreach (var pair in CurrencyMap)
            {
                if (value.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }

            return string.Empty;
        }

        private static string RemoveCurrencySymbols(string value)
        {
            var normalized = value;
            foreach (var symbol in CurrencyMap.Keys.OrderByDescending(k => k.Length))
            {
                normalized = Regex.Replace(normalized, Regex.Escape(symbol), string.Empty, RegexOptions.IgnoreCase);
            }

            return normalized;
        }

        private static string NormalizeDecimalSeparators(string value)
        {
            var lastDot = value.LastIndexOf('.');
            var lastComma = value.LastIndexOf(',');

            if (lastDot != lastComma && lastDot >= 0 && lastComma >= 0)
            {
                var separatorIndex = Math.Max(lastDot, lastComma);
                var integerPart = Regex.Replace(value[..separatorIndex], "[^0-9-]", string.Empty);
                var fractionPart = Regex.Replace(value[(separatorIndex + 1)..], "[^0-9]", string.Empty);
                return $"{integerPart}.{fractionPart}";
            }

            var cleaned = Regex.Replace(value, "[^0-9.,-]", string.Empty);
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var separatorCount = cleaned.Count(c => c == ',' || c == '.');

            if (separatorCount > 1)
            {
                cleaned = cleaned.Replace(",", string.Empty).Replace(".", string.Empty);
            }

            if (decimalSeparator == ',')
            {
                cleaned = cleaned.Replace(',', '.');
            }

            return cleaned;
        }
    }
}
