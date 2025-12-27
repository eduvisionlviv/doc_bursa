using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class UkrsibBankService
    {
        private readonly HttpClient _httpClient;

        public UkrsibBankService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.ukrsibbank.com");
        }

        public async Task<ApiResult<List<Transaction>>> FetchTransactionsAsync(string apiToken, DateTime from, DateTime to)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                var response = await _httpClient.GetAsync($"/api/v1/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return ApiResult<List<Transaction>>.FromError(
                        $"Ukrsibbank API error: {(int)response.StatusCode} {response.StatusCode}. {body}",
                        response.StatusCode,
                        new List<Transaction>());
                }

                var json = await response.Content.ReadAsStringAsync();
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
            catch (HttpRequestException httpEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка мережі Ukrsibbank: {httpEx.Message}", null, new List<Transaction>());
            }
            catch (TaskCanceledException canceledEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Таймаут Ukrsibbank: {canceledEx.Message}", null, new List<Transaction>());
            }
            catch (Exception ex)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка Ukrsibbank: {ex.Message}", null, new List<Transaction>());
            }
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
