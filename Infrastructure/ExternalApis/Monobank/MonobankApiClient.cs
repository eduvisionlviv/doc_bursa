using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace doc_bursa.Infrastructure.ExternalApis.Monobank
{
    public class MonobankApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public MonobankApiClient(string token, HttpClient? httpClient = null, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token must be provided", nameof(token));
            }

            _httpClient = httpClient ?? MonobankHttpClientFactory.Create(token);
            _logger = logger ?? CreateDefaultLogger();
        }

        public async Task<MonobankUserInfoDto> GetUserInfoAsync(CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "personal/client-info");
            return await SendAndDeserializeAsync<MonobankUserInfoDto>(request, cancellationToken);
        }

        public async Task<IReadOnlyList<MonobankTransactionDto>> GetTransactionsAsync(
            string accountId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id must be provided", nameof(accountId));
            }

            var fromUnix = new DateTimeOffset(from).ToUnixTimeSeconds();
            var toUnix = new DateTimeOffset(to).ToUnixTimeSeconds();
            var endpoint = $"personal/statement/{accountId}/{fromUnix}/{toUnix}";
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            return await SendAndDeserializeAsync<List<MonobankTransactionDto>>(request, cancellationToken);
        }

        public async Task<IReadOnlyList<MonobankCurrencyRateDto>> GetCurrencyRatesAsync(CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "bank/currency");
            return await SendAndDeserializeAsync<List<MonobankCurrencyRateDto>>(request, cancellationToken);
        }

        private async Task<T> SendAndDeserializeAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.Information("Monobank API Request {@Method} {@Url}", request.Method, request.RequestUri);

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
                _logger.Information("Monobank API Response {@StatusCode} for {@Url}", response.StatusCode, request.RequestUri);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error(ex, "Monobank API timeout for {@Url}", request.RequestUri);
                throw new MonobankApiException("Request to Monobank API timed out", HttpStatusCode.RequestTimeout);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Network error calling Monobank API for {@Url}", request.RequestUri);
                throw new MonobankApiException("Network error while calling Monobank API", HttpStatusCode.ServiceUnavailable);
            }

            if (response is null)
            {
                throw new MonobankApiException("No response from Monobank API", HttpStatusCode.ServiceUnavailable);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new MonobankRateLimitException("Monobank API rate limit exceeded");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new MonobankInvalidTokenException("Monobank API token is invalid or missing permissions");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MonobankApiException(
                    $"Monobank API returned {(int)response.StatusCode}",
                    response.StatusCode);
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var result = await JsonSerializer.DeserializeAsync<T>(contentStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);

            if (result == null)
            {
                throw new MonobankApiException("Failed to deserialize Monobank API response", response.StatusCode);
            }

            return result;
        }

        private static ILogger CreateDefaultLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("logs/monobank-api-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
    }
}
