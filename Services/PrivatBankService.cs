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
    public class PrivatBankService
    {
        private readonly HttpClient _httpClient;

        public PrivatBankService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<Transaction>> FetchTransactionsAsync(string clientId, string clientSecret, DateTime from, DateTime to)
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
                var response = await _httpClient.PostAsync("https://api.privatbank.ua/p24api/rest_fiz", content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"PrivatBank API error: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
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
