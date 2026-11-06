@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Self-elevate: if not already admin, relaunch via PowerShell.
net session >nul 2>&1
if not errorlevel 1 goto :elevated

echo [Vulcan] Requesting administrator privileges...
set "ARGS=%*"
if not defined ARGS goto :elevate_plain
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%ARGS%' -Verb RunAs"
exit /b %errorlevel%

:elevate_plain
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b %errorlevel%

:elevated

set "QUIET=0"
if /I "%~1"=="/quiet" set "QUIET=1"

tasklist /FI "IMAGENAME eq SLDWORKS.exe" 2>nul | find /I "SLDWORKS.exe" >nul
if not errorlevel 1 (
  echo [Vulcan] Please close SolidWorks before installing Vulcan.
  if not "%QUIET%"=="1" pause
  exit /b 3
)

if not exist "%~dp0Vulcan.SolidWorksClient.dll" (
  echo [Vulcan] Vulcan.SolidWorksClient.dll not found.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

if not exist "%~dp0RegAsm.exe" (
  echo [Vulcan] RegAsm.exe not found.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

echo [Vulcan] Registering add-in...
"%~dp0RegAsm.exe" "%~dp0Vulcan.SolidWorksClient.dll" /codebase
if errorlevel 1 (
  echo [Vulcan] Registration failed.
  if not "%QUIET%"=="1" pause
  exit /b 1
)

echo [Vulcan] Add-in registered. Restart SolidWorks.
if not "%QUIET%"=="1" pause
exit /b 0
