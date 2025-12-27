using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FinDesk.Infrastructure.ExternalApis.Monobank;
using RichardSzalay.MockHttp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace FinDesk.Tests
{
    public class MonobankApiClientTests
    {
        private const string Token = "test-token";
        private readonly Uri _baseAddress = new("https://api.monobank.ua/");

        [Fact]
        public async Task GetUserInfoAsync_ReturnsData()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, $"{_baseAddress}personal/client-info")
                .Respond("application/json", @"{
  ""name"": ""Test User"",
  ""permissions"": ""ps"",
  ""accounts"": [
    { ""id"": ""acc1"", ""balance"": 10000, ""creditLimit"": 0, ""currencyCode"": 980, ""cashbackType"": ""UAH"" }
  ]
}");

            var client = CreateClient(mockHttp);
            var logger = CreateTestLogger(out var sink);
            var api = new MonobankApiClient(Token, client, logger);

            var result = await api.GetUserInfoAsync();

            Assert.Equal("Test User", result.Name);
            Assert.Single(result.Accounts);
            Assert.True(sink.Events.Any());
        }

        [Fact]
        public async Task GetTransactionsAsync_MapsList()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, $"{_baseAddress}personal/statement/acc1/*")
                .Respond("application/json", @"[
  { ""id"": ""tx1"", ""time"": 1700000000, ""description"": ""Coffee"", ""amount"": -7500, ""currencyCode"": 980, ""operationAmount"": -7500, ""mcc"": 5814, ""originalMcc"": 5814, ""hold"": false, ""balance"": 100000 }
]");

            var client = CreateClient(mockHttp);
            var api = new MonobankApiClient(Token, client, CreateTestLogger(out _));

            var transactions = await api.GetTransactionsAsync("acc1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

            Assert.Single(transactions);
            var domain = MonobankTransactionMapper.ToDomain(transactions[0], "acc1");
            Assert.Equal("tx1", domain.TransactionId);
            Assert.Equal(-75.00m, domain.Amount);
            Assert.Equal("Monobank:acc1", domain.Source);
        }

        [Fact]
        public async Task RateLimit_ThrowsCustomException()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, $"{_baseAddress}personal/client-info")
                .Respond(HttpStatusCode.TooManyRequests);

            var api = new MonobankApiClient(Token, CreateClient(mockHttp), CreateTestLogger(out _));

            await Assert.ThrowsAsync<MonobankRateLimitException>(() => api.GetUserInfoAsync());
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task InvalidToken_ThrowsCustomException(HttpStatusCode status)
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, $"{_baseAddress}personal/client-info")
                .Respond(status);

            var api = new MonobankApiClient(Token, CreateClient(mockHttp), CreateTestLogger(out _));

            await Assert.ThrowsAsync<MonobankInvalidTokenException>(() => api.GetUserInfoAsync());
        }

        [Fact]
        public async Task NetworkFailure_WrapsAsApiException()
        {
            var handler = new FailingHandler();
            var client = new HttpClient(handler) { BaseAddress = _baseAddress };

            var api = new MonobankApiClient(Token, client, CreateTestLogger(out _));

            var ex = await Assert.ThrowsAsync<MonobankApiException>(() => api.GetUserInfoAsync());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        }

        [Fact]
        public async Task Timeout_WrapsAsApiException()
        {
            var handler = new DelayedHandler();
            var client = new HttpClient(handler)
            {
                BaseAddress = _baseAddress,
                Timeout = TimeSpan.FromMilliseconds(100)
            };

            var api = new MonobankApiClient(Token, client, CreateTestLogger(out _));

            var ex = await Assert.ThrowsAsync<MonobankApiException>(() => api.GetUserInfoAsync());
            Assert.Equal(HttpStatusCode.RequestTimeout, ex.StatusCode);
        }

        private HttpClient CreateClient(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler) { BaseAddress = _baseAddress };
            client.DefaultRequestHeaders.Add("X-Token", Token);
            return client;
        }

        private static ILogger CreateTestLogger(out CollectingSink sink)
        {
            sink = new CollectingSink();
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink)
                .CreateLogger();
        }

        private class CollectingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new();

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }

        private class FailingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Network error");
            }
        }

        private class DelayedHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                await Task.Delay(200, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                };
            }
        }
    }
}

