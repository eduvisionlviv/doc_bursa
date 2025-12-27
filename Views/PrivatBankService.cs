using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    public class PrivatBankService
    {
        private readonly string _token;
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://acp.privatbank.ua/api";

        public PrivatBankService(string token)
        {
            _token = token;
            _httpClient = new HttpClient();
            // Встановлюємо обов'язкові заголовки згідно документації
            _httpClient.DefaultRequestHeaders.Add("token", _token);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FinDesk AutoClient");
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Отримання виписки (транзакцій) за рахунками.
        /// </summary>
        public async Task<List<Transaction>> GetTransactionsAsync(DateTime startDate, DateTime endDate, string account = null)
        {
            var result = new List<Transaction>();
            string followId = null;
            bool existNextPage = true;

            // Формат дати: ДД-ММ-РРРР
            string startStr = startDate.ToString("dd-MM-yyyy");
            string endStr = endDate.ToString("dd-MM-yyyy");

            while (existNextPage)
            {
                var queryParams = new List<string>
                {
                    $"startDate={startStr}",
                    $"endDate={endStr}",
                    "limit=100" // Рекомендовано не більше 100
                };

                if (!string.IsNullOrEmpty(account))
                {
                    queryParams.Add($"acc={account}");
                }

                if (!string.IsNullOrEmpty(followId))
                {
                    queryParams.Add($"followId={followId}");
                }

                string url = $"{BaseUrl}/statements/transactions?{string.Join("&", queryParams)}";

                try 
                {
                    var response = await _httpClient.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Помилка API ({response.StatusCode}): {errorBody}");
                    }

                    // Читаємо відповідь. Приват може повернути cp1251, але зазвичай JSON це UTF8.
                    // Якщо будуть проблеми з кодуванням, тут треба використати Encoding.GetEncoding(1251)
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(json);

                    if (data["status"]?.ToString() != "SUCCESS")
                    {
                        throw new Exception($"API повернув помилку: {json}");
                    }

                    // Обробка пагінації
                    existNextPage = (bool?)data["exist_next_page"] ?? false;
                    followId = data["next_page_id"]?.ToString();

                    var transactionsArray = data["transactions"] as JArray;
                    if (transactionsArray != null)
                    {
                        foreach (var item in transactionsArray)
                        {
                            result.Add(MapTransaction(item));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Логування помилки можна додати тут
                    System.Diagnostics.Debug.WriteLine($"PB Import Error: {ex.Message}");
                    throw;
                }
            }

            return result;
        }

        private Transaction MapTransaction(JToken item)
        {
            // Мапінг згідно документації "Приклад відповіді з транзакціями"
            
            // Парсинг дати і часу: "07.01.2020 02:58:00"
            string dateTimeStr = item["DATE_TIME_DAT_OD_TIM_P"]?.ToString();
            DateTime date;
            if (!DateTime.TryParseExact(dateTimeStr, "dd.MM.yyyy HH:mm:ss", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out date))
            {
                // Фоллбек на дату валютування, якщо повна дата не розпарсилась
                DateTime.TryParse(item["DAT_OD"]?.ToString(), out date);
            }

            // Сума. Якщо TRANTYPE == 'D' (Дебет), то це витрата (від'ємна сума в нашому обліку)
            decimal amount = item["SUM_E"] != null ? decimal.Parse(item["SUM_E"].ToString().Replace(".", ",")) : 0;
            string type = item["TRANTYPE"]?.ToString(); // "C" (Credit) або "D" (Debit)
            
            // У виписці "C" - це надходження коштів на рахунок, "D" - списання.
            // Якщо потрібен знак мінус для витрат:
            if (type == "D")
            {
                amount = -Math.Abs(amount);
            }

            return new Transaction
            {
                TransactionId = item["ID"]?.ToString(),
                Date = date,
                Amount = amount,
                // OSND - призначення платежу
                Description = item["OSND"]?.ToString(),
                // AUT_CNTR_NAM - назва контрагента
                Counterparty = item["AUT_CNTR_NAM"]?.ToString(),
                // AUT_MY_ACC - наш рахунок
                Account = item["AUT_MY_ACC"]?.ToString(),
                Category = "Некатегоризовано",
                Source = "PrivatBank",
                // HASH для унікальності
                Hash = $"{item["ID"]}_{item["REF"]}",
                OriginalTransactionId = item["REF"]?.ToString()
            };
        }
    }
}
