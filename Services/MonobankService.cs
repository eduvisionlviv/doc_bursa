using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    public class MonobankService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<DiscoveredAccount>> _discoveryCache = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private DateTime _lastDiscoveryTime = DateTime.MinValue;

        public MonobankService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<DiscoveredAccount>> DiscoverAccountsAsync(string token)
        {
            if (_discoveryCache.ContainsKey(token) && (DateTime.Now - _lastDiscoveryTime) < _cacheDuration)
            {
                return _discoveryCache[token];
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Token", token);

            try
            {
                var response = await _httpClient.GetAsync("https://api.monobank.ua/personal/client-info");
                response.EnsureSuccessStatusCode();
                
                var accounts = new List<DiscoveredAccount>(); 

                _discoveryCache[token] = accounts;
                _lastDiscoveryTime = DateTime.Now;

                return accounts;
            }
            catch
            {
                return new List<DiscoveredAccount>();
            }
        }

        private long ToUnix(DateTime date)
        {
            return ((DateTimeOffset)date).ToUnixTimeSeconds();
        }
    }
}
