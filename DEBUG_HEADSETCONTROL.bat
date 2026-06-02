@echo off
setlocal
cd /d "%~dp0"
set EXE=%cd%\tools\headsetcontrol\headsetcontrol.exe
if not exist "%EXE%" (
  echo Missing: %EXE%
  echo Run INSTALL_HEADSETCONTROL.bat or place headsetcontrol.exe in that folder.
  pause
  exit /b 1
)
echo === HeadsetControl raw test === > headsetcontrol-debug.txt
"%EXE%" -b -o json >> headsetcontrol-debug.txt 2>&1
echo. >> headsetcontrol-debug.txt
echo === Alternative args === >> headsetcontrol-debug.txt
"%EXE%" -b --output json >> headsetcontrol-debug.txt 2>&1
type headsetcontrol-debug.txt
pause
