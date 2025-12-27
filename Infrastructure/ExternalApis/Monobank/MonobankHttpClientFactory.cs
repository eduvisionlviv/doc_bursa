using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

namespace FinDesk.Infrastructure.ExternalApis.Monobank
{
    public static class MonobankHttpClientFactory
    {
        private static readonly Uri BaseAddress = new("https://api.monobank.ua/");

        public static HttpClient Create(string token)
        {
            var policy = BuildRetryPolicy();
            var handler = new PolicyHttpMessageHandler(policy)
            {
                InnerHandler = new HttpClientHandler()
            };

            var client = new HttpClient(handler)
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Token", token);

            return client;
        }

        private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, delay, attempt, _) =>
                    {
                        // The caller is expected to log the attempt; no logging here to keep factory pure.
                    });
        }
    }
}

