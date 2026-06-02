$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host "Publishing Universal Battery Overlay single EXE..." -ForegroundColor Cyan

dotnet publish .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true

Write-Host "" 
Write-Host "Publish OK." -ForegroundColor Green
Write-Host "EXE folder:" -ForegroundColor Yellow
Write-Host ".\src\UniversalBatteryOverlay\bin\Release\net8.0-windows\win-x64\publish"
