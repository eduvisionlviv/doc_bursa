using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using doc_bursa.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;

namespace doc_bursa.Services
{
    public class PrivatBankService
    {
        private const string BaseUrl = "https://acp.privatbank.ua/api";
        private static readonly LruCache<string, List<Transaction>> _cache = new(capacity: 64, defaultTtl: TimeSpan.FromMinutes(10));
        private static readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => !response.IsSuccessStatusCode)
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        private static readonly Dictionary<string, (DateTime expiresAt, List<DiscoveredAccount> accounts)> DiscoveryCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public async Task<List<Transaction>> GetTransactionsAsync(string token, string clientId, DateTime from, DateTime to)
        {
            string cacheKey = $"{token}:{clientId}:{from:O}:{to:O}";
            return await _cache.GetOrAddAsync(cacheKey, async () =>
            {
                var transactions = new List<Transaction>();
                string accParam = string.IsNullOrWhiteSpace(clientId) ? "" : $"&acc={clientId}";
                string startDate = from.ToString("dd-MM-yyyy");
                string endDate = to.ToString("dd-MM-yyyy");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("token", token);
                client.DefaultRequestHeaders.Add("User-Agent", "FinDesk Client");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string? followId = null;
                bool existNextPage = true;

                while (existNextPage)
                {
                    var followParam = followId != null ? $"&followId={followId}" : "";
                    string url = $"{BaseUrl}/statements/transactions?startDate={startDate}&endDate={endDate}&limit=100{accParam}{followParam}";

                    var response = await _retryPolicy.ExecuteAsync(() => client.GetAsync(url));
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync();
                        throw new Exception($"PrivatBank API Error ({response.StatusCode}): {err}");
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(json);

                    if (data == null || data["status"]?.ToString() != "SUCCESS")
                        throw new Exception($"API Error: {json}");

                    var transArray = data["transactions"] as JArray;
                    if (transArray != null)
                    {
                        foreach (var item in transArray)
                        {
                            if (item != null)
                            {
                                transactions.Add(MapTransaction(item));
                            }
                        }
                    }

                    existNextPage = (bool?)data["exist_next_page"] ?? false;
                    followId = data["next_page_id"]?.ToString();
                }

                return transactions;
            });
        }

        private Transaction MapTransaction(JToken item)
        {
            string dateStr = item["DATE_TIME_DAT_OD_TIM_P"]?.ToString() ?? item["DAT_OD"]?.ToString() ?? string.Empty;
            if (!DateTime.TryParse(dateStr, out DateTime date)) date = DateTime.Now;

            decimal amount = 0;
            if (item["SUM_E"] != null)
            {
                decimal.TryParse(item["SUM_E"]!.ToString(), out amount);
            }
            
            if (item["TRANTYPE"]?.ToString() == "D") 
            {
                amount = -Math.Abs(amount);
            }

            return new Transaction
            {
                TransactionId = item["ID"]?.ToString() ?? Guid.NewGuid().ToString(),
                Date = date,
                Amount = amount,
                Description = item["OSND"]?.ToString() ?? string.Empty,
                Counterparty = item["AUT_CNTR_NAM"]?.ToString() ?? string.Empty,
                Account = item["AUT_MY_ACC"]?.ToString() ?? string.Empty,
                Source = "PrivatBank",
                Category = "Некатегоризовано",
                Hash = $"{item["ID"]}_{item["REF"]}"
            };
        }

        public async Task<List<DiscoveredAccount>> DiscoverAccountsAsync(string token, string clientId)
        {
            var cacheKey = $"{token}-{clientId}";
            if (TryGetCached(cacheKey, out var cached))
            {
                return cached;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<DiscoveredAccount>();
            }

            try
            {
                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/personal/client-info");
                request.Headers.Add("token", token);
                request.Headers.Add("clientId", clientId ?? string.Empty);
                request.Headers.Add("User-Agent", "FinDesk Client");

                var response = await client.SendAsync(request);
                var accounts = new List<DiscoveredAccount>();

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var parsed = JsonConvert.DeserializeObject<JObject>(json);
                    var items = parsed?["accounts"] as JArray;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var id = item?["id"]?.ToString() ?? Guid.NewGuid().ToString();
                            accounts.Add(new DiscoveredAccount
                            {
                                Id = id,
                                DisplayName = item?["name"]?.ToString() ?? $"PrivatBank {id}",
                                Iban = item?["iban"]?.ToString(),
                                Currency = item?["currency"]?.ToString()
                            });
                        }
                    }
                }

                if (!accounts.Any())
                {
                    accounts.Add(new DiscoveredAccount
                    {
                        Id = string.IsNullOrWhiteSpace(clientId) ? Guid.NewGuid().ToString() : clientId,
                        DisplayName = "PrivatBank рахунок",
                        Currency = "UAH"
                    });
                }

                SetCache(cacheKey, accounts);
                return accounts;
            }
            catch
            {
                var fallback = new List<DiscoveredAccount>
                {
                    new DiscoveredAccount
                    {
                        Id = string.IsNullOrWhiteSpace(clientId) ? Guid.NewGuid().ToString() : clientId,
                        DisplayName = "PrivatBank рахунок",
                        Currency = "UAH"
                    }
                };
                SetCache(cacheKey, fallback);
                return fallback;
            }
        }

        private static bool TryGetCached(string key, out List<DiscoveredAccount> accounts)
        {
            if (DiscoveryCache.TryGetValue(key, out var cache) && cache.expiresAt > DateTime.UtcNow)
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

        private static void SetCache(string key, List<DiscoveredAccount> accounts)
        {
            DiscoveryCache[key] = (DateTime.UtcNow.Add(CacheDuration), accounts);
        }
    }
}
