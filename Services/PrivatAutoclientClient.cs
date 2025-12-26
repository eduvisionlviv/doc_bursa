using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class PrivatAutoclientClient
{
    private readonly HttpClient _http = new();

    // Реальні ендпоінти/схеми можуть відрізнятися залежно від акаунта та конфігурації Приват24 для бізнесу.
    // Тут — каркас: UI дозволяє зберегти токен/id і виконати "ping", а якщо не вийшло — імпорт CSV/XLSX.
    public async Task<bool> TestAsync(string baseUrl, string token, string clientId)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            _http.BaseAddress = new Uri(baseUrl);
            using var req = new HttpRequestMessage(HttpMethod.Get, "/");
            req.Headers.Add("Authorization", $"Bearer {token}");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
