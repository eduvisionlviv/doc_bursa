using System;
using System.Collections.Generic;
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

        public async Task<List<Transaction>> FetchTransactionsAsync(string apiToken, DateTime from, DateTime to)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

                var response = await _httpClient.GetAsync($"/api/v1/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ukrsibbank API error: {response.StatusCode}");
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

                return transactions;
            }
            catch
            {
                return new List<Transaction>();
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
