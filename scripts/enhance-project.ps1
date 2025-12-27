<#
.SYNOPSIS
    Застосовує критичні патчі для завершення основного функціоналу FinDesk
.DESCRIPTION
    Розширює існуючі сервіси без створення нових файлів
.NOTES
    Запускати з кореня проекту: .\scripts\enhance-project.ps1
#>

param(
    [switch]$WhatIf,
    [switch]$Verbose
)

# Встановлюємо ErrorActionPreference на Continue, щоб не зупинятися на першій помилці
$ErrorActionPreference = "Continue"

Write-Host "🚀 FinDesk - Застосування критичних патчів" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Створення директорії для патчів одразу
$patchesDir = "patches"
if (-not (Test-Path $patchesDir)) {
    New-Item -ItemType Directory -Path $patchesDir -Force | Out-Null
    Write-Host "📁 Створено директорію patches" -ForegroundColor Green
}

# Функція для логування помилок
function Write-ErrorLog {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] ERROR: $Message"
    Write-Host $logMessage -ForegroundColor Red
    Add-Content -Path "patches/errors.log" -Value $logMessage
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
        $errorMsg = "Файл не знайдено: $FilePath"
        Write-ErrorLog $errorMsg
        return $false
    }
    
    # Створення резервної копії
    $backupPath = "$FilePath.backup"
    if (-not (Test-Path $backupPath)) {
        try {
            Copy-Item $FilePath $backupPath -Force
            Write-Host "   💾 Резервна копія: $backupPath" -ForegroundColor Gray
        } catch {
            Write-ErrorLog "Не вдалося створити резервну копію: $_"
        }
    }
    
    if ($WhatIf) {
        $lineCount = $PatchContent.Split("`n").Count
        Write-Host "   📋 Буде додано $lineCount рядків" -ForegroundColor Cyan
        return $true
    }
    
    try {
        Add-Content -Path $FilePath -Value $PatchContent -Encoding UTF8
        Write-Host "   ✅ Патч застосовано" -ForegroundColor Green
        return $true
    } catch {
        Write-ErrorLog "Помилка застосування патча: $_"
        return $false
    }
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
"@

$null = Apply-Patch -FilePath "Services/CsvImportService.cs" -PatchContent $csvImportPatch -Description "Розширений CSV Import з автовизначенням банків"

# === ПАТЧ 2: ML Категоризація (partial клас) ===
Write-Host "`n=== ПАТЧ 2: ML Категоризація ===" -ForegroundColor Magenta

$mlFilePath = "Services/CategorizationService.ML.cs"
if (-not (Test-Path $mlFilePath)) {
    $mlContent = @"
// <auto-generated />
// ML.NET категоризація для CategorizationService

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не вдалося ініціалізувати ML модель, буде використано тільки правила");
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
                    .Append(_mlContext.MulticlassClassification
