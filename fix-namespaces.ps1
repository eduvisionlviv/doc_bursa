# Скрипт для вирівнювання namespace в проекті doc_bursa
# Цей скрипт змінює всі namespace з FinDesk.* на doc_bursa.*

$projectPath = "D:\a\doc_bursa\doc_bursa"

# Список файлів для оновлення
$filesToUpdate = @(
    "Models\AccountGroup.cs",
    "Models\DataSource.cs", 
    "Models\MasterGroup.cs",
    "Models\Account.cs",
    "Models\Budget.cs",
    "Models\Category.cs",
    "Models\RecurringTransaction.cs"
)

foreach ($file in $filesToUpdate) {
    $fullPath = Join-Path $projectPath $file
    
    if (Test-Path $fullPath) {
        Write-Host "Оновлюю файл: $file"
        
        # Читаємо вміст файлу
        $content = Get-Content $fullPath -Raw -Encoding UTF8
        
        # Замінюємо namespace
        $content = $content -replace 'namespace\s+FinDesk\.Models', 'namespace doc_bursa.Models'
        
        # Додаємо using директиву, якщо її немає
        if ($content -notmatch 'using\s+doc_bursa\.Models;') {
            $content = $content -replace '(using System[^;]*;[\r\n]+)', "`$1using doc_bursa.Models;`r`n"
        }
        
        # Зберігаємо файл
        [System.IO.File]::WriteAllText($fullPath, $content, [System.Text.UTF8Encoding]::new($false))
        
        Write-Host "✓ Файл $file оновлено" -ForegroundColor Green
    }
    else {
        Write-Host "✗ Файл не знайдено: $fullPath" -ForegroundColor Red
    }
}

Write-Host "`nГотово! Тепер виконайте: dotnet build FinDesk.csproj -c Release --no-restore" -ForegroundColor Cyan
