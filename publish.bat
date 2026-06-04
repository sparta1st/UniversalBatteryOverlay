@echo off
setlocal
cd /d "%~dp0"

echo Publishing Universal Battery Overlay...
echo This creates ONE portable EXE that can be copied anywhere.
echo.

set "PROJECT=src\UniversalBatteryOverlay\UniversalBatteryOverlay.csproj"
set "DIST=dist"
set "PORTABLE=dist\UniversalBatteryOverlay.exe"
set "COPYONLY=UniversalBatteryOverlay_Portable.exe"

if exist "%DIST%" rmdir /s /q "%DIST%"
if exist "%COPYONLY%" del /f /q "%COPYONLY%"
mkdir "%DIST%" >nul 2>nul

echo Restoring for win-x64...
dotnet restore "%PROJECT%" --configfile .\NuGet.config -r win-x64
if errorlevel 1 goto failed

echo Publishing single-file self-contained EXE...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  --no-restore ^
  -o "%DIST%" ^
  /p:PublishSingleFile=true ^
  /p:SelfContained=true ^
  /p:UseAppHost=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:IncludeAllContentForSelfExtract=true ^
  /p:EnableCompressionInSingleFile=false ^
  /p:PublishTrimmed=false ^
  /p:DebugType=None ^
  /p:DebugSymbols=false
if errorlevel 1 goto failed

if not exist "%PORTABLE%" goto failed
copy /y "%PORTABLE%" "%COPYONLY%" >nul
if errorlevel 1 goto failed

echo.
echo [OK] Portable EXE created:
echo   %COPYONLY%
echo.
echo Copy ONLY this file to Desktop:
echo   UniversalBatteryOverlay_Portable.exe
echo.
echo Do NOT copy EXEs from bin\Release. Those are not the final portable build.
echo Logs are saved in %%APPDATA%%\UniversalBatteryOverlay\logs
echo.
pause
exit /b 0

:failed
echo.
echo [ERROR] Publish failed.
echo.
pause
exit /b 1
