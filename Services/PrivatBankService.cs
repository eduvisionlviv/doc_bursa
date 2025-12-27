using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    public class PrivatBankService
    {
        private const string BaseUrl = "https://acp.privatbank.ua/api";

        public async Task<List<Transaction>> GetTransactionsAsync(string token, string clientId, DateTime from, DateTime to)
        {
            var transactions = new List<Transaction>();
            string accParam = string.IsNullOrWhiteSpace(clientId) ? "" : $"&acc={clientId}";
            string startDate = from.ToString("dd-MM-yyyy");
            string endDate = to.ToString("dd-MM-yyyy");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("token", token);
                client.DefaultRequestHeaders.Add("User-Agent", "FinDesk Client");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string followId = null;
                bool existNextPage = true;

                while (existNextPage)
                {
                    var followParam = followId != null ? $"&followId={followId}" : "";
                    string url = $"{BaseUrl}/statements/transactions?startDate={startDate}&endDate={endDate}&limit=100{accParam}{followParam}";

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync();
                        throw new Exception($"PrivatBank API Error ({response.StatusCode}): {err}");
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(json);

                    if (data["status"]?.ToString() != "SUCCESS")
                        throw new Exception($"API Error: {json}");

                    var transArray = data["transactions"] as JArray;
                    if (transArray != null)
                    {
                        foreach (var item in transArray)
                        {
                            transactions.Add(MapTransaction(item));
                        }
                    }

                    existNextPage = (bool?)data["exist_next_page"] ?? false;
                    followId = data["next_page_id"]?.ToString();
                }
            }
            return transactions;
        }

        private Transaction MapTransaction(JToken item)
        {
            string dateStr = item["DATE_TIME_DAT_OD_TIM_P"]?.ToString() ?? item["DAT_OD"]?.ToString();
            if (!DateTime.TryParse(dateStr, out DateTime date)) date = DateTime.Now;

            decimal amount = item["SUM_E"] != null ? decimal.Parse(item["SUM_E"].ToString()) : 0;
            if (item["TRANTYPE"]?.ToString() == "D") amount = -Math.Abs(amount);

            return new Transaction
            {
                TransactionId = item["ID"]?.ToString(),
                Date = date,
                Amount = amount,
                Description = item["OSND"]?.ToString(),
                Counterparty = item["AUT_CNTR_NAM"]?.ToString(),
                Account = item["AUT_MY_ACC"]?.ToString(),
                Source = "PrivatBank",
                Category = "Некатегоризовано",
                Hash = $"{item["ID"]}_{item["REF"]}"
            };
        }
    }
}
