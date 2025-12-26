using ClosedXML.Excel;
using CsvHelper;
using FinDesk.Models;
using FinDesk.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class ImportService
{
    private readonly CategorizationService _categorizer;

    public ImportService(CategorizationService categorizer) => _categorizer = categorizer;

    public async Task<List<Transaction>> ImportAsync(string path, string sourceLabel = "import")
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".csv" or ".txt") return await ImportCsvAsync(path, sourceLabel);
        if (ext is ".xlsx") return await ImportXlsxAsync(path, sourceLabel);
        throw new InvalidOperationException("Підтримуються лише CSV або XLSX.");
    }

    private async Task<List<Transaction>> ImportCsvAsync(string path, string sourceLabel)
    {
        await using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        using var csv = new CsvReader(sr, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToArray() ?? Array.Empty<string>();

        string Find(params string[] variants)
        {
            foreach (var v in variants)
            {
                var h = headers.FirstOrDefault(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase));
                if (h is not null) return h;
            }
            return "";
        }

        // Heuristics across UA bank exports
        var hDate = Find("Date", "Дата", "Дата операції", "Дата операції (Київ)");
        var hAmount = Find("Amount", "Сума", "Сума операції", "Сума (UAH)", "Сума в валюті рахунку");
        var hDesc = Find("Description", "Опис", "Призначення", "Деталі", "Comment");
        var hMerchant = Find("Merchant", "Контрагент", "Одержувач", "Назва точки", "Назва торговця");
        var hCurrency = Find("Currency", "Валюта", "Currency code");
        var hAccount = Find("Account", "Рахунок", "IBAN");

        var list = new List<Transaction>();
        while (csv.Read())
        {
            var dateStr = hDate.Length > 0 ? (csv.GetField(hDate) ?? "") : "";
            var amountStr = hAmount.Length > 0 ? (csv.GetField(hAmount) ?? "") : "";
            var desc = hDesc.Length > 0 ? (csv.GetField(hDesc) ?? "") : "";
            var merch = hMerchant.Length > 0 ? (csv.GetField(hMerchant) ?? "") : "";
            var ccy = hCurrency.Length > 0 ? (csv.GetField(hCurrency) ?? "") : "UAH";
            var acc = hAccount.Length > 0 ? (csv.GetField(hAccount) ?? "") : "";

            if (!TryParseDate(dateStr, out var dtLocal)) continue;
            if (!TryParseDecimal(amountStr, out var amount)) continue;

            var t = new Transaction
            {
                Source = sourceLabel,
                Account = acc,
                DateUtc = DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime(),
                Description = desc,
                Merchant = merch,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(ccy) ? "UAH" : ccy.Trim()
            };

            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await _categorizer.GuessAsync(t);
            list.Add(t);
        }
        return list;
    }

    private async Task<List<Transaction>> ImportXlsxAsync(string path, string sourceLabel)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();

        // Find header row (first row that has "date"/"дата" and "amount"/"сума")
        var headerRow = 1;
        var maxScan = Math.Min(20, ws.LastRowUsed().RowNumber());
        for (int r = 1; r <= maxScan; r++)
        {
            var rowText = string.Join(" ", ws.Row(r).Cells(1, 20).Select(c => (c.GetString() ?? "").Trim().ToLowerInvariant()));
            if (rowText.Contains("дата") && (rowText.Contains("сума") || rowText.Contains("amount")))
            {
                headerRow = r;
                break;
            }
        }

        var headerCells = ws.Row(headerRow).CellsUsed().ToList();
        var headers = headerCells.Select(c => c.GetString().Trim()).ToList();

        int Col(params string[] variants)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                foreach (var v in variants)
                    if (string.Equals(headers[i], v, StringComparison.OrdinalIgnoreCase))
                        return headerCells[i].Address.ColumnNumber;
            }
            return -1;
        }

        var cDate = Col("Date", "Дата", "Дата операції", "Дата");
        var cAmount = Col("Amount", "Сума", "Сума операції", "Сума в валюті рахунку");
        var cDesc = Col("Description", "Опис", "Призначення", "Деталі");
        var cMerchant = Col("Merchant", "Контрагент", "Одержувач", "Назва точки", "Назва торговця");
        var cCurrency = Col("Currency", "Валюта");
        var cAccount = Col("Account", "Рахунок", "IBAN");

        var list = new List<Transaction>();
        var lastRow = ws.LastRowUsed().RowNumber();
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var dateStr = cDate > 0 ? ws.Cell(r, cDate).GetValue<string>() : "";
            var amountStr = cAmount > 0 ? ws.Cell(r, cAmount).GetValue<string>() : "";
            var desc = cDesc > 0 ? ws.Cell(r, cDesc).GetValue<string>() : "";
            var merch = cMerchant > 0 ? ws.Cell(r, cMerchant).GetValue<string>() : "";
            var ccy = cCurrency > 0 ? ws.Cell(r, cCurrency).GetValue<string>() : "UAH";
            var acc = cAccount > 0 ? ws.Cell(r, cAccount).GetValue<string>() : "";

            if (!TryParseDate(dateStr, out var dtLocal)) continue;
            if (!TryParseDecimal(amountStr, out var amount)) continue;

            var t = new Transaction
            {
                Source = sourceLabel,
                Account = acc,
                DateUtc = DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime(),
                Description = desc,
                Merchant = merch,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(ccy) ? "UAH" : ccy.Trim()
            };

            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await _categorizer.GuessAsync(t);
            list.Add(t);
        }

        return list;
    }

    private static bool TryParseDecimal(string s, out decimal v)
    {
        s = (s ?? "").Trim();
        s = s.Replace(" ", "").Replace("\u00A0", "");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("uk-UA"), out v)
            || decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }

    private static bool TryParseDate(string s, out DateTime dt)
    {
        s = (s ?? "").Trim();
        var cultures = new[] { CultureInfo.GetCultureInfo("uk-UA"), CultureInfo.InvariantCulture };
        foreach (var c in cultures)
        {
            if (DateTime.TryParse(s, c, DateTimeStyles.AssumeLocal, out dt)) return true;
        }
        dt = default;
        return false;
    }
}
