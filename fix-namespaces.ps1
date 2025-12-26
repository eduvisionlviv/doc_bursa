# PowerShell script to fix all FinDesk namespace references to doc_bursa
# Run this script from the root of the repository

$ErrorActionPreference = "Stop"

Write-Host "Starting namespace fix..." -ForegroundColor Green

# Get all .cs files recursively
$csFiles = Get-ChildItem -Path . -Include *.cs -Recurse

foreach ($file in $csFiles) {
    Write-Host "Processing: $($file.FullName)" -ForegroundColor Yellow
    
    # Read file content
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    
    # Replace namespace declarations
    $content = $content -replace 'namespace FinDesk\.Services', 'namespace doc_bursa.Services'
    $content = $content -replace 'namespace FinDesk\.Models', 'namespace doc_bursa.Models'
    $content = $content -replace 'namespace FinDesk\.ViewModels', 'namespace doc_bursa.ViewModels'
    $content = $content -replace 'namespace FinDesk\.Converters', 'namespace doc_bursa.Converters'
    $content = $content -replace 'namespace FinDesk\.Resources', 'namespace doc_bursa.Resources'
    $content = $content -replace 'namespace FinDesk', 'namespace doc_bursa'
    
    # Replace using statements
    $content = $content -replace 'using FinDesk\.Services', 'using doc_bursa.Services'
    $content = $content -replace 'using FinDesk\.Models', 'using doc_bursa.Models'
    $content = $content -replace 'using FinDesk\.ViewModels', 'using doc_bursa.ViewModels'
    $content = $content -replace 'using FinDesk\.Converters', 'using doc_bursa.Converters'
    $content = $content -replace 'using FinDesk\.Resources', 'using doc_bursa.Resources'
    $content = $content -replace 'using FinDesk', 'using doc_bursa'
    
    # Write back to file
    Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
}

Write-Host "Namespace fix completed!" -ForegroundColor Green
Write-Host "Total files processed: $($csFiles.Count)" -ForegroundColor Cyan
