$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
Write-Host "Starting Universal Battery Overlay..." -ForegroundColor Cyan
dotnet run --project .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj
