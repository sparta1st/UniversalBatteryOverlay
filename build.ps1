$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
Write-Host "Building Universal Battery Overlay..." -ForegroundColor Cyan
dotnet build .\src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj -c Release
Write-Host "Build OK." -ForegroundColor Green
