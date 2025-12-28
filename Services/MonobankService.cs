using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using doc_bursa.Models;
using System.Linq;

namespace doc_bursa.Services
{
    public class MonobankService
    {
        private const string BaseUrl = "https://api.monobank.ua";
        private static readonly HttpClient HttpClient = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly Dictionary<string, (DateTime expiresAt, List<DiscoveredAccount> accounts)> DiscoveryCache = new();

        public async Task<List<Transaction>> GetTransactionsAsync(string token, string accountId, DateTime from, DateTime to)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Token", token);
                long fromUnix = ((DateTimeOffset)from).ToUnixTimeSeconds();
                long toUnix = ((DateTimeOffset)to).ToUnixTimeSeconds();
                string account = string.IsNullOrWhiteSpace(accountId) ? "0" : accountId;

                string url = $"{BaseUrl}/personal/statement/{account}/{fromUnix}/{toUnix}";
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429) 
                        throw new Exception("Ліміт запитів Monobank (1 раз на 60 сек). Зачекайте.");
                    throw new Exception($"Monobank Error: {response.ReasonPhrase}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var monoData = JsonConvert.DeserializeObject<List<MonoDto>>(json);
                var result = new List<Transaction>();

                if (monoData != null)
                {
                    foreach (var m in monoData)
                    {
                        // Пропускаємо, якщо немає ID
                        if (string.IsNullOrEmpty(m.id)) continue;

                        result.Add(new Transaction
                        {
                            TransactionId = m.id,
                            Date = DateTimeOffset.FromUnixTimeSeconds(m.time).LocalDateTime,
                            Amount = m.amount / 100.0m,
                            Description = m.description ?? string.Empty,
                            Source = "Monobank",
                            Category = "Некатегоризовано",
                            Hash = $"{m.id}_{m.time}"
                        });
                    }
                }
                return result;
            }
        }

        // DTO клас із підтримкою Nullable, щоб прибрати попередження
        private class MonoDto 
        { 
            public string? id { get; set; } 
            public long time { get; set; } 
            public string? description { get; set; } 
            public long amount { get; set; } 
        }

        public async Task<List<DiscoveredAccount>> DiscoverAccountsAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<DiscoveredAccount>();
            }

            if (TryGetCached(token, out var cached))
            {
                return cached;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/personal/client-info");
                request.Headers.Add("X-Token", token);
                var response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var client = JsonConvert.DeserializeObject<dynamic>(json);
                var accounts = new List<DiscoveredAccount>();

                foreach (var acc in client?.accounts ?? new List<dynamic>())
                {
                    string? accountId = acc.id;
                    string? masked = acc.maskedPan != null ? string.Join(",", acc.maskedPan.ToObject<List<string>>()) : null;
                    string? iban = acc.iban;
                    string? currency = acc.currencyCode?.ToString();

                    accounts.Add(new DiscoveredAccount
                    {
                        Id = !string.IsNullOrWhiteSpace(accountId) ? accountId : Guid.NewGuid().ToString(),
                        DisplayName = masked ?? iban ?? $"Monobank {accountId}",
                        Iban = iban,
                        Currency = currency
                    });
                }

                SetCache(token, accounts);
                return accounts;
            }
            catch
            {
                var fallback = new List<DiscoveredAccount>
                {
                    new DiscoveredAccount
                    {
                        Id = "mono-default",
                        DisplayName = "Monobank рахунок",
                        Currency = "UAH"
                    }
                };
                SetCache(token, fallback);
                return fallback;
            }
        }

        private static bool TryGetCached(string token, out List<DiscoveredAccount> accounts)
        {
            if (DiscoveryCache.TryGetValue(token, out var cache) && cache.expiresAt > DateTime.UtcNow)
            {
                accounts = cache.accounts.Select(a => new DiscoveredAccount
                {
                    Id = a.Id,
                    DisplayName = a.DisplayName,
                    Iban = a.Iban,
                    Currency = a.Currency,
                    AccountGroupId = a.AccountGroupId,
                    IsVirtual = a.IsVirtual
                }).ToList();
                return true;
            }

            accounts = new List<DiscoveredAccount>();
            return false;
        }

        private static void SetCache(string token, List<DiscoveredAccount> accounts)
        {
            DiscoveryCache[token] = (DateTime.UtcNow.Add(CacheDuration), accounts);
        }
    }
}
