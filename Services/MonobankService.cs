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
    public class MonobankService
    {
        private readonly HttpClient _httpClient;

        public MonobankService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FinDesk/1.0");
        }

        public async Task<ApiResult<List<Transaction>>> FetchTransactionsAsync(string token, DateTime from, DateTime to)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Token", token);

                var fromUnix = ((DateTimeOffset)from).ToUnixTimeSeconds();
                var toUnix = ((DateTimeOffset)to).ToUnixTimeSeconds();

                var response = await _httpClient.GetAsync($"https://api.monobank.ua/personal/statement/0/{fromUnix}/{toUnix}");

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return ApiResult<List<Transaction>>.FromError(
                        $"Monobank API error: {(int)response.StatusCode} {response.StatusCode}. {body}",
                        response.StatusCode,
                        new List<Transaction>());
                }

                var json = await response.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<List<MonobankTransaction>>(json);

                var transactions = new List<Transaction>();
                foreach (var item in items ?? new List<MonobankTransaction>())
                {
                    var transaction = new Transaction
                    {
                        TransactionId = item.id ?? Guid.NewGuid().ToString(),
                        Date = DateTimeOffset.FromUnixTimeSeconds(item.time).DateTime,
                        Amount = item.amount / 100m,
                        Description = item.description ?? "",
                        Source = "Monobank"
                    };
                    transaction.Hash = ComputeHash(transaction);
                    transactions.Add(transaction);
                }

                return ApiResult<List<Transaction>>.FromSuccess(transactions);
            }
            catch (HttpRequestException httpEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка мережі Monobank: {httpEx.Message}", null, new List<Transaction>());
            }
            catch (TaskCanceledException canceledEx)
            {
                return ApiResult<List<Transaction>>.FromError($"Таймаут Monobank: {canceledEx.Message}", null, new List<Transaction>());
            }
            catch (Exception ex)
            {
                return ApiResult<List<Transaction>>.FromError($"Помилка Monobank: {ex.Message}", null, new List<Transaction>());
            }
        }

        private string ComputeHash(Transaction t)
        {
            var data = $"{t.TransactionId}|{t.Date:O}|{t.Amount}|{t.Source}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(bytes);
        }

        private class MonobankTransaction
        {
            public string? id { get; set; }
            public long time { get; set; }
            public string? description { get; set; }
            public int amount { get; set; }
        }
    }
}
