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
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace doc_bursa.Services
{
    public class UkrsibBankService
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

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
    }
}
