using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для валідації та тестування API токенів банків
    /// </summary>
    public class ValidationService
    {
        private readonly HttpClient _httpClient;
        private readonly EncryptionService _encryption;

        public ValidationService(EncryptionService encryptionService)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _encryption = encryptionService;
        }

        /// <summary>
        /// Тестує з'єднання з API банку
        /// </summary>
        public async Task<(bool isValid, string message)> TestConnectionAsync(DataSource source)
        {
            try
            {
                var token = _encryption.Decrypt(source.ApiToken);

                switch (source.Type)
                {
                    case "Monobank":
                        return await TestMonobankAsync(token);
                    
                    case "PrivatBank":
                        return await TestPrivatBankAsync(token, source.ClientId);
                    
                    case "Ukrsibbank":
                        return await TestUkrsibbankAsync(token);
                    
                    case "CSV Import":
                        return (true, "Імпорт CSV не потребує токена");
                    
                    default:
                        return (false, "Невідомий тип банку");
                }
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Помилка підключення: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return (false, "Перевищено час очікування відповіді");
            }
            catch (Exception ex)
            {
                return (false, $"Помилка: {ex.Message}");
            }
        }

        /// <summary>
        /// Тест Monobank API
        /// </summary>
        private async Task<(bool, string)> TestMonobankAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Токен порожній");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.monobank.ua/personal/client-info");
                request.Headers.Add("X-Token", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    var name = json.RootElement.GetProperty("name").GetString();
                    return (true, $"Підключення успішне! Клієнт: {name}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Недійсний токен");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return (false, "Занадто багато запитів. Почекайте 60 секунд");
                }
                else
                {
                    return (false, $"Помилка API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Помилка: {ex.Message}");
            }
        }

        /// <summary>
        /// Тест PrivatBank API
        /// </summary>
        private async Task<(bool, string)> TestPrivatBankAsync(string token, string clientId)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Токен порожній");

            if (string.IsNullOrWhiteSpace(clientId))
                return (false, "Не вказано ClientId");

            try
            {
                // PrivatBank API використовує токен + клієнт ID
                var url = "https://acp.privatbank.ua/api/statements/transactions";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("token", token);
                request.Headers.Add("clientId", clientId);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Підключення до PrivatBank успішне!");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Недійсний токен або ClientId");
                }
                else
                {
                    return (false, $"Помилка API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Помилка: {ex.Message}");
            }
        }

        /// <summary>
        /// Тест Ukrsibbank API
        /// </summary>
        private async Task<(bool, string)> TestUkrsibbankAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Токен порожній");

            // TODO: Реалізувати після отримання документації API
            return await Task.FromResult((true, "Ukrsibbank API ще не реалізовано"));
        }

        /// <summary>
        /// Валідує формат токена
        /// </summary>
        public bool ValidateTokenFormat(string token, string bankType)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            switch (bankType)
            {
                case "Monobank":
                    // Monobank токени звичайно 50+ символів
                    return token.Length >= 50;

                case "PrivatBank":
                    // PrivatBank токени UUID формат
                    return token.Length >= 32;

                case "Ukrsibbank":
                    return token.Length >= 20;

                default:
                    return true;
            }
        }
    }
}

