using FinDesk.Models;
using FinDesk.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class MonobankClient
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://api.monobank.ua")
    };

    private sealed class MonoClientInfo
    {
        public List<MonoAccount> accounts { get; set; } = new();
    }

    private sealed class MonoAccount
    {
        public string id { get; set; } = "";
        public string currencyCode { get; set; } = "";
        public string iban { get; set; } = "";
        public string type { get; set; } = "";
    }

    private sealed class MonoStatement
    {
        public long time { get; set; } // unix seconds
        public string description { get; set; } = "";
        public long mcc { get; set; }
        public long amount { get; set; }       // in minor units
        public long operationAmount { get; set; }
        public int currencyCode { get; set; }
        public string comment { get; set; } = "";
        public string counterName { get; set; } = "";
    }

    public async Task<List<(string accountId, string iban)>> GetAccountsAsync(string monoToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/personal/client-info");
        req.Headers.Add("X-Token", monoToken);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var info = await resp.Content.ReadFromJsonAsync<MonoClientInfo>();
        var list = new List<(string, string)>();
        if (info?.accounts is null) return list;

        foreach (var a in info.accounts)
        {
            if (!string.IsNullOrWhiteSpace(a.id))
                list.Add((a.id, a.iban ?? ""));
        }
        return list;
    }

    public async Task<List<Transaction>> GetStatementsAsync(
        string monoToken,
        string accountId,
        DateTime fromUtc,
        DateTime toUtc,
        string accountLabel,
        CategorizationService categorizer)
    {
        // Monobank statement endpoint expects unix seconds.
        var from = new DateTimeOffset(fromUtc).ToUnixTimeSeconds();
        var to = new DateTimeOffset(toUtc).ToUnixTimeSeconds();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/personal/statement/{accountId}/{from}/{to}");
        req.Headers.Add("X-Token", monoToken);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<MonoStatement>>(json) ?? new();

        var txs = new List<Transaction>();
        foreach (var s in items)
        {
            // Monobank returns amounts in minor units (kopeks).
            var amount = s.amount / 100m;
            var dt = DateTimeOffset.FromUnixTimeSeconds(s.time).UtcDateTime;

            var merchant = string.IsNullOrWhiteSpace(s.counterName) ? s.description : s.counterName;
            var desc = string.IsNullOrWhiteSpace(s.comment) ? s.description : $"{s.description} • {s.comment}";

            var t = new Transaction
            {
                Source = "monobank",
                Account = string.IsNullOrWhiteSpace(accountLabel) ? accountId : accountLabel,
                DateUtc = dt,
                Merchant = merchant ?? "",
                Description = desc ?? "",
                Amount = amount,
                Currency = "UAH",
                RawJson = json
            };
            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await categorizer.GuessAsync(t);
            txs.Add(t);
        }
        return txs;
    }

    public IEnumerable<(DateTime fromUtc, DateTime toUtc)> ChunkBy31Days(DateTime fromUtc, DateTime toUtc)
    {
        // For safety with provider limits; caller can split large periods.
        var cur = fromUtc;
        while (cur < toUtc)
        {
            var next = cur.AddDays(31);
            if (next > toUtc) next = toUtc;
            yield return (cur, next);
            cur = next.AddSeconds(1);
        }
    }
}
