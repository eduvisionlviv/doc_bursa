<#
.SYNOPSIS
    Застосовує критичні патчі для завершення основного функціоналу FinDesk
.DESCRIPTION
    Розширює існуючі сервіси без створення нових файлів:
    - CsvImportService: автовизначення формату банків
    - CategorizationService: ML.NET категоризація
    - DeduplicationService: розумна дедуплікація
    - BudgetService: сповіщення про перевищення бюджету
.NOTES
    Запускати з кореня проекту: .\scripts\enhance-project.ps1
#>

param(
    [switch]$WhatIf,  # Показати, що буде змінено, не застосовуючи
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "🚀 FinDesk - Застосування критичних патчів" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Перевірка наявності git
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Git не встановлено" -ForegroundColor Red
    exit 1
}

# Перевірка, що ми в корені репозиторію
if (-not (Test-Path ".git")) {
    Write-Host "❌ Це не Git репозиторій. Перейдіть в директорію проекту" -ForegroundColor Red
    exit 1
}

# Створення директорії для патчів
$patchesDir = "patches"
if (-not (Test-Path $patchesDir)) {
    New-Item -ItemType Directory -Path $patchesDir -Force | Out-Null
    Write-Host "📁 Створено директорію patches" -ForegroundColor Green
}

# Функція для застосування патча
function Apply-Patch {
    param(
        [string]$FilePath,
        [string]$PatchContent,
        [string]$Description
    )
    
    Write-Host "📝 $Description" -ForegroundColor Yellow
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "   ⚠️  Файл не знайдено: $FilePath" -ForegroundColor Yellow
        return $false
    }
    
    # Створення резервної копії
    $backupPath = "$FilePath.backup"
    if (-not (Test-Path $backupPath)) {
        Copy-Item $FilePath $backupPath -Force
        Write-Host "   💾 Резервна копія: $backupPath" -ForegroundColor Gray
    }
    
    if ($WhatIf) {
        Write-Host "   📋 Буде додано $($PatchContent.Split("`n").Count) рядків" -ForegroundColor Cyan
        return $true
    }
    
    # Додаємо патч в кінець файлу (для простоти)
    # Для складніших патчів можна використовувати більш точну логіку
    Add-Content -Path $FilePath -Value $PatchContent -Encoding UTF8
    Write-Host "   ✅ Патч застосовано" -ForegroundColor Green
    return $true
}

# === ПАТЧ 1: Розширений CSV Import ===
Write-Host "`n=== ПАТЧ 1: Розширений CSV Import ===" -ForegroundColor Magenta

$csvImportPatch = @"

// === РОЗШИРЕНИЙ CSV IMPORT (додано 2025-12-27) ===

// Внутрішній клас для конфігурації банків
private class BankCsvFormat
{
    public string BankName { get; set; }
    public string[] RequiredHeaders { get; set; }
    public string[] DateFormats { get; set; }
    public System.Text.Encoding Encoding { get; set; }
    public string AmountColumn { get; set; }
    public string DescriptionColumn { get; set; }
    public string DateColumn { get; set; }
}

// Список підтримуваних банків
private readonly Dictionary<string, BankCsvFormat> _bankFormats = new()
{
    ["monobank"] = new BankCsvFormat
    {
        BankName = "Monobank",
        RequiredHeaders = new[] { "Дата", "Опис", "Сума", "Валюта" },
        DateFormats = new[] { "dd.MM.yyyy", "yyyy-MM-dd" },
        Encoding = System.Text.Encoding.UTF8,
        AmountColumn = "Сума",
        DescriptionColumn = "Опис",
        DateColumn = "Дата"
    },
    ["privatbank"] = new BankCsvFormat
    {
        BankName = "PrivatBank",
        RequiredHeaders = new[] { "Дата операції", "Опис", "Сума", "Валюта" },
        DateFormats = new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy" },
        Encoding = System.Text.Encoding.GetEncoding(1251),
        AmountColumn = "Сума",
        DescriptionColumn = "Опис",
        DateColumn = "Дата операції"
    },
    ["ukrsibbank"] = new BankCsvFormat
    {
        BankName = "Ukrsibbank",
        RequiredHeaders = new[] { "Дата", "Назва", "Сума", "Валюта" },
        DateFormats = new[] { "dd.MM.yyyy", "yyyy-MM-dd" },
        Encoding = System.Text.Encoding.UTF8,
        AmountColumn = "Сума",
        DescriptionColumn = "Назва",
        DateColumn = "Дата"
    },
    ["pumb"] = new BankCsvFormat
    {
        BankName = "ПУМБ",
        RequiredHeaders = new[] { "Дата", "Назва", "Сума", "Валюта" },
        DateFormats = new[] { "dd.MM.yyyy", "yyyy-MM-dd" },
        Encoding = System.Text.Encoding.UTF8,
        AmountColumn = "Сума",
        DescriptionColumn = "Назва",
        DateColumn = "Дата"
    },
    ["oshchad"] = new BankCsvFormat
    {
        BankName = "Ощадбанк",
        RequiredHeaders = new[] { "Дата", "Опис", "Сума", "Валюта" },
        DateFormats = new[] { "dd.MM.yyyy", "yyyy-MM-dd" },
        Encoding = System.Text.Encoding.UTF8,
        AmountColumn = "Сума",
        DescriptionColumn = "Опис",
        DateColumn = "Дата"
    }
};

// Автовизначення формату банку
private async Task<BankCsvFormat> DetectBankFormatAsync(string filePath)
{
    foreach (var encoding in new[] { System.Text.Encoding.UTF8, System.Text.Encoding.GetEncoding(1251), System.Text.Encoding.Latin1 })
    {
        try
        {
            var lines = await System.IO.File.ReadAllLinesAsync(filePath, encoding);
            if (lines.Length < 2) continue;

            var headers = lines[0].ToLower().Split(',').Select(h => h.Trim()).ToArray();
            
            foreach (var format in _bankFormats.Values)
            {
                var matchScore = format.RequiredHeaders.Count(required => 
                    headers.Any(h => h.Contains(required.ToLower())));
                
                if (matchScore >= format.RequiredHeaders.Length * 0.7)
                {
                    format.Encoding = encoding;
                    return format;
                }
            }
        }
        catch { /* Пробуємо наступне кодування */ }
    }
    return null;
}

// Парсинг одного рядка за форматом банку
private Transaction ParseRow(CsvHelper.CsvReader csv, BankCsvFormat format)
{
    try
    {
        var dateStr = csv.GetField(format.DateColumn);
        var description = csv.GetField(format.DescriptionColumn);
        var amountStr = csv.GetField(format.AmountColumn);

        if (string.IsNullOrWhiteSpace(dateStr) || string.IsNullOrWhiteSpace(description))
            return null;

        return new Transaction
        {
            Description = description.Trim(),
            Amount = ParseAmount(amountStr),
            Currency = csv.GetField("Валюта") ?? "UAH",
            Timestamp = ParseDate(dateStr, format.DateFormats),
            Source = format.BankName,
            CreatedAt = DateTime.UtcNow
        };
    }
    catch
    {
        return null;
    }
}

private decimal ParseAmount(string amountStr)
{
    if (string.IsNullOrWhiteSpace(amountStr)) return 0;
    amountStr = amountStr.Replace(" ", "").Replace("\"", "");
    if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount))
        return amount;
    return 0;
}

private DateTime ParseDate(string dateStr, string[] formats)
{
    foreach (var format in formats)
       {
        if (DateTime.TryParseExact(dateStr, format, System.Globalization.CultureInfo.InvariantCulture, 
            System.Globalization.DateTimeStyles.None, out var date))
            return date;
    }
    if (DateTime.TryParse(dateStr, out var fallbackDate))
        return fallbackDate;
    return DateTime.UtcNow;
}

// Модифікуйте ImportCsvAsync, щоб використовувати DetectBankFormatAsync
// Приклад:
// var bankFormat = await DetectBankFormatAsync(filePath);
// if (bankFormat == null) return (0, 0, "Не вдалося визначити формат");
"@

Apply-Patch -FilePath "Services/CsvImportService.cs" -PatchContent $csvImportPatch -Description "Розширений CSV Import з автовизначенням банків"

# === ПАТЧ 2: ML Категоризація (partial клас) ===
Write-Host "`n=== ПАТЧ 2: ML Категоризація ===" -ForegroundColor Magenta

# Створюємо partial файл для ML
$mlFilePath = "Services/CategorizationService.ML.cs"
if (-not (Test-Path $mlFilePath)) {
    $mlContent = @"
// <auto-generated />
// ML.NET категоризація для CategorizationService

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
+using System.Threading.Tasks;
using FinDesk.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FinDesk.Services
{
    public partial class CategorizationService
    {
        private MLContext _mlContext;
        private ITransformer _mlModel;
        private string _modelPath = "models/categorization_model.zip";

        private async Task InitializeMLModelAsync()
        {
            _mlContext = new MLContext(seed: 0);
            
            if (File.Exists(_modelPath))
            {
                _mlModel = _mlContext.Model.Load(_modelPath, out _);
                _logger.LogInformation("ML модель завантажено");
            }
            else
            {
                await TrainModelAsync();
            }
        }

        private async Task TrainModelAsync()
        {
            try
            {
                var transactions = await _dbService.GetTransactionsAsync();
                var categorized = transactions.Where(t => t.CategoryId.HasValue).Take(500).ToList();
                
                if (categorized.Count < 100)
                {
                    _logger.LogWarning("Недостатньо даних для навчання ML моделі (потрібно мінімум 100 транзакцій)");
                    return;
                }

                var data = categorized.Select(t => new TransactionData
                {
                    Description = t.Description,
                    CategoryId = t.CategoryId.Value
                }).ToList();

                var dataView = _mlContext.Data.LoadFromEnumerable(data);
                var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(TransactionData.Description))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                _mlModel = pipeline.Fit(dataView);
                
                Directory.CreateDirectory(Path.GetDirectoryName(_modelPath));
                _mlContext.Model.Save(_mlModel, dataView.Schema, _modelPath);
                _logger.LogInformation($"ML модель навчено на {categorized.Count} транзакціях");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка навчання ML моделі");
            }
        }

        private async Task<Category> ApplyMLCategorizationAsync(string description)
        {
            if (_mlModel == null) return null;

            try
            {
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<TransactionData, CategoryPrediction>(_mlModel);
                var input = new TransactionData { Description = description };
                var prediction = predictionEngine.Predict(input);

                if (prediction.Score.Max() > 0.6) // 60% впевненості
                {
                    var categories = await _dbService.GetCategoriesAsync();
                    return categories.FirstOrDefault(c => c.Id == prediction.CategoryId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML предикт не вдався");
            }

            return null;
        }

        private class TransactionData
        {
            public string Description { get; set; }
+            public int CategoryId { get; set; }
        }

        private class CategoryPrediction
        {
            [ColumnName("PredictedLabel")]
            public int CategoryId { get; set; }
            public float[] Score { get; set; }
        }
    }
}
"@

    if ($WhatIf) {
        Write-Host "   📋 Буде створено файл: $mlFilePath" -ForegroundColor Cyan
    } else {
        Set-Content -Path $mlFilePath -Value $mlContent -Encoding UTF8
        Write-Host "   ✅ Створено CategorizationService.ML.cs" -ForegroundColor Green
    }
} else {
    Write-Host "   ⚠️  Файл вже існує: $mlFilePath" -ForegroundColor Yellow
}

# === ПАТЧ 3: Розумна дедуплікація ===
Write-Host "`n=== ПАТЧ 3: Розумна дедуплікація ===" -ForegroundColor Magenta

$dedupPatch = @"

// === РОЗУМНА ДЕДУПЛІКАЦІЯ (додано 2025-12-27) ===

// Алгоритм Левенштейна
private int LevenshteinDistance(string s, string t)
{
    if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
    if (string.IsNullOrEmpty(t)) return s.Length;

    int[,] d = new int[s.Length + 1, t.Length + 1];

    for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
    for (int j = 0; j <= t.Length; j++) d[0, j] = j;

    for (int i = 1; i <= s.Length; i++)
    {
        for (int j = 1; j <= t.Length; j++)
        {
            int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }
    }

    return d[s.Length, t.Length];
}

// Багатофакторна оцінка схожості
private double GetSimilarityScore(Transaction a, Transaction b)
{
    if (a == null || b == null) return 0;

    // 1. Схожість опису (50% ваги)
    double descSimilarity = 1.0 - (double)LevenshteinDistance(a.Description, b.Description) / 
                           Math.Max(a.Description.Length, b.Description.Length);
    descSimilarity = Math.Max(0, descSimilarity);

    // 2. Схожість суми (30% ваги)
    double amountSimilarity = 1.0 - Math.Abs((double)(a.Amount - b.Amount)) / 
                              Math.Max(Math.Abs((double)a.Amount), Math.Abs((double)b.Amount));
    amountSimilarity = Math.Max(0, amountSimilarity);

    // 3. Схожість дати (20% ваги)
    int daysDiff = Math.Abs((a.Timestamp - b.Timestamp).Days);
    double dateSimilarity = daysDiff <= 3 ? 1.0 : (daysDiff <= 7 ? 0.5 : 0);

    return descSimilarity * 0.5 + amountSimilarity * 0.
