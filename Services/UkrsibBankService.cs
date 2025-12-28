using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using System.Linq;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace doc_bursa.Services
{
    public class UkrsibBankService
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;
        private static readonly Dictionary<string, (DateTime expiresAt, List<DiscoveredAccount> accounts)> DiscoveryCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public UkrsibBankService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.ukrsibbank.com"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _resiliencePolicy = BuildPolicy();
        }

        public async Task<ApiResult<List<Transaction>>> FetchTransactionsAsync(string apiToken, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                var response = await _resiliencePolicy.ExecuteAsync(
                    ct => _httpClient.GetAsync($"/api/v1/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ct),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return ApiResult<List<Transaction>>.FromError(
                        $"Ukrsibbank API error: {(int)response.StatusCode} {response.StatusCode}. {body}",
                        response.StatusCode,
                        new List<Transaction>());
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var items = JsonSerializer.Deserialize<List<UkrsibTransaction>>(json);

                var transactions = new List<Transaction>();
                foreach (var item in items ?? new List<UkrsibTransaction>())
                {
                    var transaction = new Transaction
                    {
                        TransactionId = item.id ?? Guid.NewGuid().ToString(),
                        Date = DateTime.Parse(item.date ?? DateTime.Now.ToString("O")),
                        Amount = item.amount,
                        Description = item.description ?? "",
                        Source = "Ukrsibbank"
                    };
                    transaction.Hash = ComputeHash(transaction);
                    transactions.Add(transaction);
                }

                return ApiResult<List<Transaction>>.FromSuccess(transactions);
            }
            catch (TaskCanceledException canceledEx) when (!cancellationToken.IsCancellationRequested)
            {
                return ApiResult<List<Transaction>>.FromError($"Таймаут Ukrsibbank: {canceledEx.Message}", null, new List<Transaction>());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException httpEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка мережі Ukrsibbank: {httpEx.Message}", null, new List<Transaction>());
            }
            catch (Exception ex)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка Ukrsibbank: {ex.Message}", null, new List<Transaction>());
            }
        }

        private static IAsyncPolicy<HttpResponseMessage> BuildPolicy()
        {
            var retry = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
            return Policy.WrapAsync(timeout, retry);
        }

        private string ComputeHash(Transaction t)
        {
            var data = $"{t.TransactionId}|{t.Date:O}|{t.Amount}|{t.Source}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(bytes);
        }

        private class UkrsibTransaction
        {
            public string? id { get; set; }
            public string? date { get; set; }
            public decimal amount { get; set; }
            public string? description { get; set; }
        }

        public async Task<List<DiscoveredAccount>> DiscoverAccountsAsync(string apiToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return new List<DiscoveredAccount>();
            }

            if (TryGetCached(apiToken, out var cached))
            {
                return cached;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
                var response = await _resiliencePolicy.ExecuteAsync(ct => _httpClient.GetAsync("/api/v1/accounts", ct), cancellationToken);
                var accounts = new List<DiscoveredAccount>();
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                    foreach (var item in data ?? new List<Dictionary<string, object>>())
                    {
                        var id = item.TryGetValue("id", out var identifier) ? identifier?.ToString() : Guid.NewGuid().ToString();
                        item.TryGetValue("iban", out var ibanObj);
                        item.TryGetValue("currency", out var currencyObj);

                        accounts.Add(new DiscoveredAccount
                        {
                            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id!,
                            DisplayName = item.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "Ukrsibbank account" : "Ukrsibbank account",
                            Iban = ibanObj?.ToString(),
                            Currency = currencyObj?.ToString()
                        });
                    }
                }

                if (!accounts.Any())
                {
                    accounts.Add(new DiscoveredAccount
                    {
                        Id = "ukrsib-default",
                        DisplayName = "Ukrsibbank рахунок",
                        Currency = "UAH"
                    });
                }

                SetCache(apiToken, accounts);
                return accounts;
            }
            catch
            {
                var fallback = new List<DiscoveredAccount>
                {
                    new DiscoveredAccount
                    {
                        Id = "ukrsib-default",
                        DisplayName = "Ukrsibbank рахунок",
                        Currency = "UAH"
                    }
                };
                SetCache(apiToken, fallback);
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
