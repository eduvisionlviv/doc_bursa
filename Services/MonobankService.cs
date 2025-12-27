using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    public class MonobankService
    {
        private const string BaseUrl = "https://api.monobank.ua";

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
                        result.Add(new Transaction
                        {
                            TransactionId = m.id,
                            Date = DateTimeOffset.FromUnixTimeSeconds(m.time).LocalDateTime,
                            Amount = m.amount / 100.0m,
                            Description = m.description,
                            Source = "Monobank",
                            Category = "Некатегоризовано",
                            Hash = $"{m.id}_{m.time}"
                        });
                    }
                }
                return result;
            }
        }

        private class MonoDto { public string id { get; set; } public long time { get; set; } public string description { get; set; } public long amount { get; set; } }
    }
}
