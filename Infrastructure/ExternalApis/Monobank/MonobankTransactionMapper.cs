using System;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.ExternalApis.Monobank
{
    public static class MonobankTransactionMapper
    {
        public static Transaction ToDomain(MonobankTransactionDto dto, string accountId)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Transaction
            {
                TransactionId = dto.Id,
                Date = DateTimeOffset.FromUnixTimeSeconds(dto.Time).UtcDateTime,
                Amount = ConvertMinorUnitsToDecimal(dto.Amount),
                Description = BuildDescription(dto),
                Category = "Інше",
                Source = $"Monobank:{accountId}",
                Hash = string.Empty
            };
        }

        private static decimal ConvertMinorUnitsToDecimal(long minorUnits)
        {
            return minorUnits / 100m;
        }

        private static string BuildDescription(MonobankTransactionDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Comment))
            {
                return $"{dto.Description} ({dto.Comment})";
            }

            return dto.Description;
        }
    }
}
