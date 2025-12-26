using FinDesk.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class Db
{
    private readonly AppSettings _settings;

    public Db(AppSettings settings) => _settings = settings;

    private SqliteConnection Open() => new($"Data Source={_settings.DbPath}");

    public async Task InitAsync()
    {
        await using var con = Open();
        await con.OpenAsync();

        var cmd = con.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS transactions (
  id TEXT PRIMARY KEY,
  source TEXT NOT NULL,
  account TEXT NOT NULL,
  dateUtc TEXT NOT NULL,
  description TEXT NOT NULL,
  merchant TEXT NOT NULL,
  amount REAL NOT NULL,
  currency TEXT NOT NULL,
  category INTEGER NOT NULL,
  hash TEXT NOT NULL UNIQUE,
  rawJson TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS merchant_category (
  merchantNorm TEXT PRIMARY KEY,
  category INTEGER NOT NULL,
  updatedUtc TEXT NOT NULL
);
";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpsertTransactionsAsync(IEnumerable<Transaction> txs)
    {
        var inserted = 0;
        await using var con = Open();
        await con.OpenAsync();

        await using var tr = await con.BeginTransactionAsync();
        foreach (var t in txs)
        {
            var cmd = con.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = @"
INSERT OR IGNORE INTO transactions
(id, source, account, dateUtc, description, merchant, amount, currency, category, hash, rawJson)
VALUES ($id, $source, $account, $dateUtc, $description, $merchant, $amount, $currency, $category, $hash, $rawJson);
";
            cmd.Parameters.AddWithValue("$id", t.Id);
            cmd.Parameters.AddWithValue("$source", t.Source);
            cmd.Parameters.AddWithValue("$account", t.Account);
            cmd.Parameters.AddWithValue("$dateUtc", t.DateUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$description", t.Description);
            cmd.Parameters.AddWithValue("$merchant", t.Merchant);
            cmd.Parameters.AddWithValue("$amount", t.Amount);
            cmd.Parameters.AddWithValue("$currency", t.Currency);
            cmd.Parameters.AddWithValue("$category", (int)t.Category);
            cmd.Parameters.AddWithValue("$hash", t.Hash);
            cmd.Parameters.AddWithValue("$rawJson", t.RawJson ?? "");
            var n = await cmd.ExecuteNonQueryAsync();
            inserted += n;
        }
        await tr.CommitAsync();
        return inserted;
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var list = new List<Transaction>();
        await using var con = Open();
        await con.OpenAsync();

        var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, source, account, dateUtc, description, merchant, amount, currency, category, hash, rawJson
FROM transactions
WHERE dateUtc >= $from AND dateUtc <= $to
ORDER BY dateUtc DESC;
";
        cmd.Parameters.AddWithValue("$from", fromUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$to", toUtc.ToString("o"));

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Transaction
            {
                Id = r.GetString(0),
                Source = r.GetString(1),
                Account = r.GetString(2),
                DateUtc = DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Description = r.GetString(4),
                Merchant = r.GetString(5),
                Amount = r.GetDecimal(6),
                Currency = r.GetString(7),
                Category = (MoneyCategory)r.GetInt32(8),
                Hash = r.GetString(9),
                RawJson = r.GetString(10),
            });
        }
        return list;
    }

    public async Task SaveMerchantCategoryAsync(string merchantNorm, MoneyCategory category)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
INSERT INTO merchant_category(merchantNorm, category, updatedUtc)
VALUES($m, $c, $u)
ON CONFLICT(merchantNorm) DO UPDATE SET
  category = excluded.category,
  updatedUtc = excluded.updatedUtc;
";
        cmd.Parameters.AddWithValue("$m", merchantNorm);
        cmd.Parameters.AddWithValue("$c", (int)category);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<MoneyCategory?> TryGetMerchantCategoryAsync(string merchantNorm)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT category FROM merchant_category WHERE merchantNorm = $m LIMIT 1;";
        cmd.Parameters.AddWithValue("$m", merchantNorm);
        var obj = await cmd.ExecuteScalarAsync();
        if (obj is null || obj is DBNull) return null;
        return (MoneyCategory)Convert.ToInt32(obj);
    }

    public async Task UpdateTransactionCategoryAsync(string txId, MoneyCategory cat)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET category = $c WHERE id = $id;";
        cmd.Parameters.AddWithValue("$c", (int)cat);
        cmd.Parameters.AddWithValue("$id", txId);
        await cmd.ExecuteNonQueryAsync();
    }
}
