using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinDesk.Models;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace FinDesk.Services
{
    public class PrivatBankService
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

        public PrivatBankService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _resiliencePolicy = BuildPolicy();
        }

        public async Task<ApiResult<List<Transaction>>> FetchTransactionsAsync(string clientId, string clientSecret, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    merchant = new { id = clientId, signature = clientSecret },
                    operation = "rest",
                    payment = new
                    {
                        startDate = from.ToString("dd.MM.yyyy"),
                        endDate = to.ToString("dd.MM.yyyy")
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _resiliencePolicy.ExecuteAsync(
                    ct => _httpClient.PostAsync("https://api.privatbank.ua/p24api/rest_fiz", content, ct),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    return ApiResult<List<Transaction>>.FromError(
                        $"PrivatBank API error: {(int)response.StatusCode} {response.StatusCode}. {body}",
                        response.StatusCode,
                        new List<Transaction>());
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<PrivatBankResponse>(json);

                var transactions = new List<Transaction>();
                foreach (var item in result?.data?.statements ?? new List<PrivatBankStatement>())
                {
                    var transaction = new Transaction
                    {
                        TransactionId = item.orderReference ?? Guid.NewGuid().ToString(),
                        Date = DateTime.ParseExact(item.transactionDate ?? DateTime.Now.ToString("dd.MM.yyyy"), "dd.MM.yyyy", null),
                        Amount = decimal.Parse(item.amount ?? "0"),
                        Description = item.description ?? "",
                        Source = "PrivatBank"
                    };
                    transaction.Hash = ComputeHash(transaction);
                    transactions.Add(transaction);
                }

                return ApiResult<List<Transaction>>.FromSuccess(transactions);
            }
            catch (TaskCanceledException canceledEx) when (!cancellationToken.IsCancellationRequested)
            {
                return ApiResult<List<Transaction>>.FromError($"Таймаут PrivatBank: {canceledEx.Message}", null, new List<Transaction>());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException httpEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка мережі PrivatBank: {httpEx.Message}", null, new List<Transaction>());
            }
            catch (Exception ex)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка PrivatBank: {ex.Message}", null, new List<Transaction>());
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

        private class PrivatBankResponse
        {
            public PrivatBankData? data { get; set; }
        }

        private class PrivatBankData
        {
            public List<PrivatBankStatement>? statements { get; set; }
        }

        private class PrivatBankStatement
        {
            public string? orderReference { get; set; }
            public string? transactionDate { get; set; }
            public string? amount { get; set; }
            public string? description { get; set; }
        }
    }
}

